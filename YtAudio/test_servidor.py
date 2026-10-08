"""
As rotas do yt-audio com o yt-dlp e o YouTube de mentira: confere o HTTP que o Lavalink
recebe — o arquivo inteiro, o pedaço do /seek, o link renovado e as recusas.
"""

import threading
import time
import unittest
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer
from unittest import mock

import servidor

ID = 'dQw4w9WgXcQ'
DADOS = bytes(range(256)) * 100


class _RespostaFalsa:
    def __init__(self, status, dados=b''):
        self.status_code = status
        self._dados = dados

    def iter_content(self, tamanho):
        for i in range(0, len(self._dados), tamanho):
            yield self._dados[i:i + tamanho]

    def close(self):
        pass


def _youtube(url, **_):
    """O googlevideo de mentira: entrega o pedaço que o &range= do link pede."""
    de, até = map(int, url.rsplit('&range=', 1)[1].split('-'))
    return _RespostaFalsa(200, DADOS[de:até + 1])


def _resolvido(vid):
    return servidor.Áudio(
        url=f'https://googlevideo.invalid/videoplayback?id={vid}',
        cabeçalhos={},
        formato='251',
        tamanho=len(DADOS),
        tipo='audio/webm',
        validade=time.time() + 3600)


class RotasTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.http = ThreadingHTTPServer(('127.0.0.1', 0), servidor.Rotas)
        cls.porta = cls.http.server_address[1]
        threading.Thread(target=cls.http.serve_forever, daemon=True).start()

    @classmethod
    def tearDownClass(cls):
        cls.http.shutdown()
        cls.http.server_close()

    def setUp(self):
        servidor._guardados.clear()
        self.addCleanup(mock.patch.stopall)
        self.resolver = mock.patch.object(servidor, '_resolver', side_effect=_resolvido).start()
        self.get = mock.patch.object(servidor.requests, 'get', side_effect=_youtube).start()
        mock.patch.object(servidor, 'PEDAÇO', 10000).start()

    def pedir(self, caminho, cabeçalhos=None, método='GET'):
        pedido = urllib.request.Request(
            f'http://127.0.0.1:{self.porta}{caminho}', headers=cabeçalhos or {}, method=método)

        try:
            with urllib.request.urlopen(pedido, timeout=5) as resposta:
                return resposta.status, resposta.headers, resposta.read()
        except urllib.error.HTTPError as erro:
            with erro:
                return erro.code, erro.headers, erro.read()

    def test_arquivo_inteiro_em_pedaços(self):
        status, cabeçalhos, corpo = self.pedir(f'/v/{ID}')

        self.assertEqual(status, 200)
        self.assertEqual(corpo, DADOS)
        self.assertEqual(cabeçalhos['Content-Length'], str(len(DADOS)))
        self.assertEqual(cabeçalhos['Accept-Ranges'], 'bytes')
        self.assertEqual(cabeçalhos['Content-Type'], 'audio/webm')
        # 25600 bytes em pedaços de 10000: três pedidos ao YouTube
        self.assertEqual(self.get.call_count, 3)

    def test_do_meio_até_o_fim(self):
        status, cabeçalhos, corpo = self.pedir(f'/v/{ID}', {'Range': 'bytes=12345-'})

        self.assertEqual(status, 206)
        self.assertEqual(corpo, DADOS[12345:])
        self.assertEqual(cabeçalhos['Content-Range'], f'bytes 12345-{len(DADOS) - 1}/{len(DADOS)}')
        self.assertEqual(cabeçalhos['Content-Length'], str(len(DADOS) - 12345))

    def test_começo_depois_do_fim_é_416(self):
        status, cabeçalhos, _ = self.pedir(f'/v/{ID}', {'Range': f'bytes={len(DADOS)}-'})

        self.assertEqual(status, 416)
        self.assertEqual(cabeçalhos['Content-Range'], f'bytes */{len(DADOS)}')

    def test_preparar_guarda_o_link(self):
        self.assertEqual(self.pedir(f'/preparar/{ID}')[0], 204)
        self.assertEqual(self.pedir(f'/v/{ID}')[0], 200)
        self.assertEqual(self.resolver.call_count, 1)

    def test_link_recusado_ganha_um_novo(self):
        recusas = [_RespostaFalsa(403)]
        self.get.side_effect = lambda url, **kw: recusas.pop() if recusas else _youtube(url)

        status, _, corpo = self.pedir(f'/v/{ID}')

        self.assertEqual(status, 200)
        self.assertEqual(corpo, DADOS)
        self.assertEqual(self.resolver.call_count, 2)

    def test_link_novo_de_outro_formato_não_serve(self):
        self.get.side_effect = lambda url, **kw: _RespostaFalsa(403) if 'velho' in url else _youtube(url)
        velho = _resolvido(ID)
        self.resolver.side_effect = [
            servidor.Áudio(**{**velho.__dict__, 'url': velho.url + '&velho'}),
            servidor.Áudio(**{**velho.__dict__, 'formato': '140'}),
        ]

        self.assertEqual(self.pedir(f'/v/{ID}')[0], 502)

    def test_youtube_recusando_de_vez_é_502(self):
        self.get.side_effect = lambda url, **kw: _RespostaFalsa(403)

        self.assertEqual(self.pedir(f'/v/{ID}')[0], 502)

    def test_recusa_do_yt_dlp_é_502_com_o_motivo(self):
        self.resolver.side_effect = servidor.Recusa("Sign in to confirm you're not a bot")

        status, _, corpo = self.pedir(f'/v/{ID}')

        self.assertEqual(status, 502)
        self.assertIn('not a bot', corpo.decode())

    def test_head_não_pede_o_áudio(self):
        status, cabeçalhos, corpo = self.pedir(f'/v/{ID}', método='HEAD')

        self.assertEqual(status, 200)
        self.assertEqual(corpo, b'')
        self.assertEqual(cabeçalhos['Content-Length'], str(len(DADOS)))
        self.assertEqual(self.get.call_count, 0)

    def test_id_torto_é_400(self):
        self.assertEqual(self.pedir('/v/curto')[0], 400)
        self.assertEqual(self.pedir('/preparar/..%2F..%2Fetc')[0], 400)

    def test_rota_desconhecida_é_404(self):
        self.assertEqual(self.pedir('/nada')[0], 404)
        self.assertEqual(self.pedir(f'/v/{ID}/mais')[0], 404)

    def test_saude(self):
        status, _, corpo = self.pedir('/saude')

        self.assertEqual(status, 200)
        self.assertEqual(corpo, b'ok\n')


if __name__ == '__main__':
    unittest.main()
