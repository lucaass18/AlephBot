"""
As contas do yt-audio que não dependem de rede: o pedaço do arquivo que o Lavalink pediu,
até quando um link do YouTube vale e o tamanho do áudio. Ficam aqui, longe do yt-dlp e do
requests, pra dar pra testar sem YouTube e sem instalar nada.
"""

import re
from urllib.parse import parse_qs, urlparse

# o id de um vídeo do YouTube: 11 caracteres do alfabeto base64 de URL
_ID = re.compile(r'[A-Za-z0-9_-]{11}')

# "bytes=INÍCIO-FIM", "bytes=INÍCIO-" ou "bytes=-ÚLTIMOS"
_RANGE = re.compile(r'\s*bytes\s*=\s*(\d*)\s*-\s*(\d*)\s*')

# o link do áudio diz quando vence (expire=); devolvo antes disso, com folga pra uma música
# longa terminar de tocar com o link que começou
MARGEM = 30 * 60

# e nunca guardo mais que isso, mesmo que o link diga que vale mais
MÁXIMO = 5 * 60 * 60

# link sem expire= (nunca vi um) vale pouco, por garantia
SEM_PRAZO = 60 * 60

TIPOS = {
    'webm': 'audio/webm',
    'm4a': 'audio/mp4',
    'mp4': 'audio/mp4',
}


class ForaDoArquivo(Exception):
    """O pedaço pedido começa depois do fim do arquivo: é o 416 do HTTP."""


def id_válido(texto: str) -> bool:
    return _ID.fullmatch(texto) is not None


def intervalo(cabeçalho: str | None, tamanho: int) -> tuple[int, int, bool]:
    """
    O pedaço que o cliente pediu, como (início, fim, parcial), com o fim incluído.

    Sem Range é o arquivo inteiro. Range torto ou com vários pedaços também vira o arquivo
    inteiro: a RFC deixa o servidor ignorar o cabeçalho, e o Lavalink só pede um pedaço por
    vez. Pedido que começa depois do fim levanta ForaDoArquivo.
    """
    inteiro = (0, tamanho - 1, False)

    if not cabeçalho:
        return inteiro

    achado = _RANGE.fullmatch(cabeçalho)

    if achado is None:
        return inteiro

    de, até = achado.groups()

    if de == '' and até == '':
        return inteiro

    # "bytes=-500": os últimos 500
    if de == '':
        últimos = int(até)

        if últimos == 0:
            raise ForaDoArquivo()

        return max(0, tamanho - últimos), tamanho - 1, True

    início = int(de)

    if início >= tamanho:
        raise ForaDoArquivo()

    fim = min(int(até), tamanho - 1) if até != '' else tamanho - 1

    if fim < início:
        return inteiro

    return início, fim, True


def pedaços(início: int, fim: int, tamanho_do_pedaço: int):
    """
    Os pedaços (de, até) em que eu peço o áudio ao YouTube, com o fim incluído. Pedir tudo
    de uma vez faz o servidor dele segurar a velocidade; em pedaços ele entrega cheio.
    """
    de = início

    while de <= fim:
        até = min(de + tamanho_do_pedaço - 1, fim)
        yield de, até
        de = até + 1


def validade(url: str, agora: float) -> float:
    """Até quando eu reaproveito este link, em segundos desde a época."""
    try:
        expira = int(parse_qs(urlparse(url).query)['expire'][0])
    except (KeyError, IndexError, ValueError):
        return agora + SEM_PRAZO

    return min(expira - MARGEM, agora + MÁXIMO)


def tamanho(formato: dict) -> int | None:
    """
    O tamanho do áudio em bytes. O yt-dlp costuma saber (filesize); quando não sabe, o
    próprio link do YouTube diz (clen=).
    """
    if isinstance(formato.get('filesize'), int) and formato['filesize'] > 0:
        return formato['filesize']

    try:
        clen = int(parse_qs(urlparse(formato.get('url') or '').query)['clen'][0])
    except (KeyError, IndexError, ValueError):
        return None

    return clen if clen > 0 else None


def tipo(formato: dict) -> str:
    """O Content-Type do áudio. O Lavalink descobre o formato lendo o começo do arquivo;
    isto é só a dica certa pra ele."""
    return TIPOS.get(formato.get('ext') or '', 'application/octet-stream')
