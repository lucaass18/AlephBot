"""Os testes das contas do yt-audio. Rodam no build da imagem: se quebrar, ela nem sai."""

import unittest

import regras


class IdTest(unittest.TestCase):
    def test_id_de_vídeo(self):
        self.assertTrue(regras.id_válido('dQw4w9WgXcQ'))
        self.assertTrue(regras.id_válido('a-b_c1234XY'))

    def test_o_que_não_é_id(self):
        for texto in ('', 'curto', 'dQw4w9WgXcQ1', '../../etc/x', 'dQw4w9WgXc!', 'dQw4w9WgXc/'):
            with self.subTest(texto=texto):
                self.assertFalse(regras.id_válido(texto))


class IntervaloTest(unittest.TestCase):
    def test_sem_range_é_o_arquivo_inteiro(self):
        self.assertEqual(regras.intervalo(None, 1000), (0, 999, False))
        self.assertEqual(regras.intervalo('', 1000), (0, 999, False))

    def test_do_meio_até_o_fim(self):
        # é o que o Lavalink manda quando você usa o /seek
        self.assertEqual(regras.intervalo('bytes=500-', 1000), (500, 999, True))

    def test_pedaço_fechado(self):
        self.assertEqual(regras.intervalo('bytes=0-99', 1000), (0, 99, True))

    def test_fim_depois_do_arquivo_é_cortado(self):
        self.assertEqual(regras.intervalo('bytes=900-5000', 1000), (900, 999, True))

    def test_últimos_bytes(self):
        self.assertEqual(regras.intervalo('bytes=-100', 1000), (900, 999, True))
        self.assertEqual(regras.intervalo('bytes=-5000', 1000), (0, 999, True))

    def test_começo_depois_do_fim_é_416(self):
        with self.assertRaises(regras.ForaDoArquivo):
            regras.intervalo('bytes=1000-', 1000)

        with self.assertRaises(regras.ForaDoArquivo):
            regras.intervalo('bytes=-0', 1000)

    def test_range_torto_vira_o_arquivo_inteiro(self):
        for cabeçalho in ('bytes=abc', 'linhas=0-10', 'bytes=0-10,20-30', 'bytes=-', 'bytes=50-10'):
            with self.subTest(cabeçalho=cabeçalho):
                self.assertEqual(regras.intervalo(cabeçalho, 1000), (0, 999, False))

    def test_espaços_não_atrapalham(self):
        self.assertEqual(regras.intervalo(' bytes = 10 - 20 ', 1000), (10, 20, True))


class PedaçosTest(unittest.TestCase):
    def test_cobre_tudo_sem_buraco_nem_sobra(self):
        self.assertEqual(list(regras.pedaços(0, 24, 10)), [(0, 9), (10, 19), (20, 24)])

    def test_pedaço_menor_que_o_tamanho(self):
        self.assertEqual(list(regras.pedaços(5, 7, 10)), [(5, 7)])

    def test_um_byte(self):
        self.assertEqual(list(regras.pedaços(3, 3, 10)), [(3, 3)])


class ValidadeTest(unittest.TestCase):
    AGORA = 1_000_000.0

    def test_devolve_antes_de_vencer(self):
        url = f'https://rr1.googlevideo.com/videoplayback?expire={int(self.AGORA) + 4 * 3600}&itag=251'
        self.assertEqual(regras.validade(url, self.AGORA), self.AGORA + 4 * 3600 - regras.MARGEM)

    def test_nunca_passa_do_máximo(self):
        url = f'https://rr1.googlevideo.com/videoplayback?expire={int(self.AGORA) + 48 * 3600}'
        self.assertEqual(regras.validade(url, self.AGORA), self.AGORA + regras.MÁXIMO)

    def test_sem_expire_vale_pouco(self):
        self.assertEqual(regras.validade('https://x/videoplayback?itag=251', self.AGORA), self.AGORA + regras.SEM_PRAZO)
        self.assertEqual(regras.validade('https://x/videoplayback?expire=amanhã', self.AGORA), self.AGORA + regras.SEM_PRAZO)


class TamanhoTest(unittest.TestCase):
    def test_filesize_do_yt_dlp(self):
        self.assertEqual(regras.tamanho({'filesize': 5139087, 'url': 'https://x/v?clen=1'}), 5139087)

    def test_clen_do_link_quando_falta_o_filesize(self):
        self.assertEqual(regras.tamanho({'filesize': None, 'url': 'https://x/v?itag=251&clen=4200000'}), 4200000)

    def test_sem_nenhum_dos_dois(self):
        self.assertIsNone(regras.tamanho({'url': 'https://x/v?itag=251'}))
        self.assertIsNone(regras.tamanho({}))


class TipoTest(unittest.TestCase):
    def test_tipos(self):
        self.assertEqual(regras.tipo({'ext': 'webm'}), 'audio/webm')
        self.assertEqual(regras.tipo({'ext': 'm4a'}), 'audio/mp4')
        self.assertEqual(regras.tipo({'ext': 'xyz'}), 'application/octet-stream')
        self.assertEqual(regras.tipo({}), 'application/octet-stream')


if __name__ == '__main__':
    unittest.main()
