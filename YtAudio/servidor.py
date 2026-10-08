"""
yt-audio: entrega ao Lavalink o áudio de um vídeo do YouTube, pego pelo yt-dlp sem conta.

De um servidor o YouTube não entrega áudio a quem não está logado. Daqui o yt-dlp sai pelo
WARP (o contêiner warp) levando um token anti-robô do gerador (o contêiner pot), e assim ele
entrega. O Lavalink toca http://yt-audio:8080/v/<id> como um link HTTP qualquer; quem manda
ele tocar isso, no lugar da faixa do YouTube, é o bot.

Rotas:
  GET|HEAD /v/<id>         o áudio, aceitando Range (é assim que o /seek pula trechos)
  GET      /preparar/<id>  descobre o link e guarda; o bot chama antes de mandar tocar
  GET      /saude          o healthcheck do compose
"""

import copy
import logging
import os
import threading
import time
from dataclasses import dataclass
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import requests
import yt_dlp
from yt_dlp.utils import DownloadError
from yt_dlp.version import __version__ as VERSÃO_DO_YT_DLP

import regras

PORTA = int(os.environ.get('PORTA', '8080'))

# por onde o yt-dlp, o gerador de token (que recebe o proxy do yt-dlp) e o próprio áudio
# saem pra internet. Tem que ser a mesma saída nos três: o link do áudio só vale pro IP que
# pediu ele
PROXY = os.environ.get('PROXY', 'socks5://warp:9091')
GERADOR = os.environ.get('GERADOR_DE_TOKEN', 'http://pot:4416')

# o client do YouTube que o yt-dlp usa. O mweb foi o que entregou a música inteira sem conta
# no teste da VPS (08/10/2026); se o YouTube fechar ele, é aqui que muda
CLIENTS = [c.strip() for c in os.environ.get('CLIENTS', 'mweb').split(',') if c.strip()]

# o pedaço que eu peço por vez ao YouTube
PEDAÇO = 10 * 1024 * 1024

# yt-dlp ao mesmo tempo: cada um sobe um deno pra resolver o desafio do player
SIMULTÂNEOS = 2

# links guardados; passando disso, o mais velho sai
GUARDADOS = 200

# um vídeo curto e eterno ("Me at the zoo"), só pra aquecer
AQUECIMENTO = 'jNQXAC9IVRw'

log = logging.getLogger('yt-audio')


@dataclass(frozen=True)
class Áudio:
    url: str
    cabeçalhos: dict
    formato: str
    tamanho: int
    tipo: str
    validade: float


class Recusa(Exception):
    """O YouTube (ou o yt-dlp) não entregou o áudio deste vídeo."""


class _LogDoYtDlp:
    """O yt-dlp fala muito. Aviso passa; erro não, porque ele volta como exceção e sai uma vez só."""

    def debug(self, mensagem):
        pass

    def info(self, mensagem):
        pass

    def warning(self, mensagem):
        log.warning('yt-dlp: %s', mensagem)

    def error(self, mensagem):
        log.debug('yt-dlp: %s', mensagem)


OPÇÕES = {
    'quiet': True,
    'noprogress': True,
    'noplaylist': True,
    'logger': _LogDoYtDlp(),
    'proxy': PROXY,
    # opus primeiro, que é o formato do Discord. Só arquivo direto (https): áudio em HLS vem
    # como uma lista de pedaços (.m3u8), e eu serviria a lista no lugar do som
    'format': 'bestaudio[acodec=opus][protocol=https]/bestaudio[protocol=https]',
    'extractor_args': {
        # fetch_pot=always: o token vai também no pedido do player, e é ele que passa pela
        # tela de "Sign in to confirm you're not a bot"
        'youtube': {'player_client': CLIENTS, 'fetch_pot': ['always']},
        'youtubepot-bgutilhttp': {'base_url': [GERADOR]},
    },
}

_PROXIES = {'http': PROXY, 'https': PROXY}

_guardados: dict[str, Áudio] = {}
_travas: dict[str, threading.Lock] = {}
_trava_geral = threading.Lock()
_vagas = threading.BoundedSemaphore(SIMULTÂNEOS)


def áudio(vid: str, renovar: bool = False) -> Áudio:
    """O áudio do vídeo: o guardado, se ainda vale, ou um novo do yt-dlp."""
    with _trava_geral:
        trava = _travas.setdefault(vid, threading.Lock())

    # um pedido por vídeo de cada vez: o /preparar do bot e o probe do Lavalink chegam juntos
    with trava:
        guardado = _guardados.get(vid)

        if guardado is not None and not renovar and guardado.validade > time.time():
            return guardado

        with _vagas:
            novo = _resolver(vid)

        with _trava_geral:
            _guardados.pop(vid, None)
            _guardados[vid] = novo
            _arrumar()

        return novo


def _arrumar():
    """Tira os vencidos e, se ainda sobrar demais, os mais velhos. Roda com a trava geral."""
    agora = time.time()

    for vid in [v for v, a in _guardados.items() if a.validade <= agora]:
        del _guardados[vid]
        _travas.pop(vid, None)

    while len(_guardados) > GUARDADOS:
        vid = next(iter(_guardados))
        del _guardados[vid]
        _travas.pop(vid, None)


def _resolver(vid: str) -> Áudio:
    começo = time.monotonic()

    try:
        with yt_dlp.YoutubeDL(copy.deepcopy(OPÇÕES)) as ydl:
            info = ydl.extract_info(f'https://www.youtube.com/watch?v={vid}', download=False)
    except DownloadError as erro:
        raise Recusa(str(erro).removeprefix('ERROR: ')) from None

    if info.get('is_live'):
        raise Recusa('transmissão ao vivo não passa por aqui')

    # com um formato só escolhido, o yt-dlp põe os dados dele no próprio info
    tamanho = regras.tamanho(info)

    if not info.get('url') or tamanho is None:
        raise Recusa(f'o formato {info.get("format_id")} veio sem link ou sem tamanho')

    novo = Áudio(
        url=info['url'],
        cabeçalhos=dict(info.get('http_headers') or {}),
        formato=str(info.get('format_id')),
        tamanho=tamanho,
        tipo=regras.tipo(info),
        validade=regras.validade(info['url'], time.time()),
    )

    log.info('%s: formato %s (%s, %.1f MB) em %.1f s',
             vid, novo.formato, info.get('ext'), tamanho / 1048576, time.monotonic() - começo)

    return novo


def _abrir(vid: str, atual: Áudio, de: int, até: int) -> tuple[requests.Response, Áudio]:
    """
    Pede um pedaço ao YouTube. Link vencido ou recusado (403) ganha um link novo, uma vez —
    e só serve se for do mesmo formato: o Lavalink já tem o começo do antigo.
    """
    for tentativa in (1, 2):
        resposta = requests.get(
            f'{atual.url}&range={de}-{até}',
            headers=atual.cabeçalhos,
            proxies=_PROXIES,
            stream=True,
            timeout=(10, 30))

        if resposta.status_code in (200, 206):
            return resposta, atual

        resposta.close()

        if resposta.status_code != 403 or tentativa == 2:
            raise IOError(f'o YouTube respondeu {resposta.status_code}')

        log.info('%s: o YouTube recusou o link guardado; peço outro', vid)
        novo = áudio(vid, renovar=True)

        if (novo.formato, novo.tamanho) != (atual.formato, atual.tamanho):
            raise IOError(f'o link novo é de outro formato ({novo.formato}, era {atual.formato})')

        atual = novo

    raise AssertionError('inalcançável')


class Rotas(BaseHTTPRequestHandler):
    protocol_version = 'HTTP/1.1'
    server_version = 'yt-audio'
    sys_version = ''

    def do_GET(self):
        self._atender(corpo=True)

    def do_HEAD(self):
        self._atender(corpo=False)

    def log_message(self, formato, *args):
        # cada pedaço do /seek viraria uma linha; o que importa já sai nos logs de cima
        log.debug('%s %s', self.address_string(), formato % args)

    def _atender(self, corpo: bool):
        partes = self.path.split('?', 1)[0].strip('/').split('/')

        if partes == ['saude']:
            return self._texto(200, 'ok', corpo)

        if len(partes) != 2 or partes[0] not in ('v', 'preparar'):
            return self._texto(404, 'rota desconhecida', corpo)

        rota, vid = partes

        if not regras.id_válido(vid):
            return self._texto(400, 'id de vídeo inválido', corpo)

        try:
            atual = áudio(vid)
        except Exception as erro:
            log.warning('%s: %s', vid, erro)
            return self._texto(502, str(erro), corpo)

        if rota == 'preparar':
            self.send_response(204)
            self.end_headers()
            return

        self._entregar(vid, atual, corpo)

    def _entregar(self, vid: str, atual: Áudio, corpo: bool):
        try:
            início, fim, parcial = regras.intervalo(self.headers.get('Range'), atual.tamanho)
        except regras.ForaDoArquivo:
            self.send_response(416)
            self.send_header('Content-Range', f'bytes */{atual.tamanho}')
            self.send_header('Content-Length', '0')
            self.end_headers()
            return

        pedaços = regras.pedaços(início, fim, PEDAÇO)
        primeiro = next(pedaços)
        resposta = None

        # o primeiro pedaço é pedido antes do cabeçalho: se o YouTube recusar de cara, ainda
        # dá pra responder 502, em vez de um 200 que morre no primeiro byte
        if corpo:
            try:
                resposta, atual = _abrir(vid, atual, *primeiro)
            except Exception as erro:
                log.warning('%s: %s', vid, erro)
                return self._texto(502, str(erro), corpo)

        self.send_response(206 if parcial else 200)
        self.send_header('Content-Type', atual.tipo)
        self.send_header('Content-Length', str(fim - início + 1))
        self.send_header('Accept-Ranges', 'bytes')

        if parcial:
            self.send_header('Content-Range', f'bytes {início}-{fim}/{atual.tamanho}')

        self.end_headers()

        if resposta is None:
            return

        try:
            self._copiar(resposta, *primeiro)

            for de, até in pedaços:
                resposta, atual = _abrir(vid, atual, de, até)
                self._copiar(resposta, de, até)
        except (BrokenPipeError, ConnectionResetError):
            # o Lavalink fechou: a música acabou, foi pulada, ou ele só leu o começo pra
            # descobrir o formato
            pass
        except Exception as erro:
            # o cabeçalho já foi; só resta cortar, e o Lavalink pede de novo de onde parou
            log.warning('%s: o áudio parou no meio: %s', vid, erro)
            self.close_connection = True

    def _copiar(self, resposta: requests.Response, de: int, até: int):
        esperado = até - de + 1
        recebido = 0

        try:
            for bloco in resposta.iter_content(64 * 1024):
                self.wfile.write(bloco)
                recebido += len(bloco)
        finally:
            resposta.close()

        if recebido != esperado:
            raise IOError(f'o YouTube mandou {recebido} de {esperado} bytes')

    def _texto(self, status: int, texto: str, corpo: bool):
        dados = (texto + '\n').encode()
        self.send_response(status)
        self.send_header('Content-Type', 'text/plain; charset=utf-8')
        self.send_header('Content-Length', str(len(dados)))
        self.end_headers()

        if corpo:
            self.wfile.write(dados)


def _aquecer():
    """
    O primeiro yt-dlp do contêiner baixa o player do YouTube, resolve o desafio dele no deno,
    e o gerador monta o BotGuard: leva bem mais que os seguintes. Faço isso já no boot, pra
    primeira música de verdade não pagar essa conta. A espera é pro pot terminar de subir.
    """
    time.sleep(5)

    try:
        áudio(AQUECIMENTO)
        log.info('aquecido')
    except Exception as erro:
        log.warning('o aquecimento falhou: %s', erro)


def main():
    logging.basicConfig(
        level=logging.INFO,
        format='%(asctime)s %(levelname)-7s %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S')

    log.info('yt-dlp %s, client %s, saída %s, gerador %s', VERSÃO_DO_YT_DLP, ','.join(CLIENTS), PROXY, GERADOR)

    threading.Thread(target=_aquecer, daemon=True).start()

    servidor = ThreadingHTTPServer(('0.0.0.0', PORTA), Rotas)
    servidor.daemon_threads = True

    log.info('ouvindo na porta %d', PORTA)
    servidor.serve_forever()


if __name__ == '__main__':
    main()
