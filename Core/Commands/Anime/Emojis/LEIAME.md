# Os emoji da ficha do /ma

Estes PNGs **não entram no build**. O Discord é quem hospeda emoji customizado: o arquivo é
subido uma vez como Application Emoji e o embed passa a referenciar `<:nome:id>`. Eles ficam
aqui por procedência e licença, e para poder subir de novo sem refazer o trabalho.

## Como subir

Developer Portal → o app do AlephBot → aba **Emojis** → arrastar os nove arquivos. O nome do
emoji tem que ser igual ao nome do arquivo, sem o `.png`, porque é por ele que o bot acha o id
no boot. Pela API dá no mesmo: `POST /applications/{id}/emojis` com `{name, image}`, a imagem
num data URI `data:image/png;base64,...`.

Faltando algum, o rótulo daquele campo sai só com a palavra. Nada quebra.

## Quem é quem

| Arquivo | Campo | Ícone no Game Icons |
|---|---|---|
| `ma_nota.png` | Nota | `round-star` |
| `ma_ranking.png` | Ranking | `laurels-trophy` |
| `ma_situacao.png` | Situação | `aerial-signal` |
| `ma_formato.png` | Formato | `tv` |
| `ma_periodo.png` | Exibição / Publicação | `calendar` |
| `ma_estudio.png` | Estúdio (anime) | `clapperboard` |
| `ma_autoria.png` | Autoria (mangá) | `quill-ink` |
| `ma_generos.png` | Gêneros | `price-tag` |
| `ma_elenco.png` | Elenco | `drama-masks` |

## Licença

Arte do [Game Icons](https://github.com/game-icons/icons), sob
[CC BY 3.0](https://github.com/game-icons/icons/blob/master/license.txt) — **a atribuição é
obrigatória**, e é o motivo deste arquivo existir. Os SVGs vieram pela API do
[Iconify](https://iconify.design), recoloridos para `#d9b35c` e rasterizados em 128×128 com
fundo transparente.

O dourado foi escolhido por ler nos dois temas do Discord: ícone escuro some no tema escuro e
claro demais some no claro.

## Como foram gerados

```
https://api.iconify.design/game-icons/{icone}.svg?color=%23d9b35c&height=112
```

O SVG vai para um HTML de 128×128 e o Edge headless tira o print com
`--default-background-color=00000000`, que é o que dá o fundo transparente. Não há dependência
nova no projeto: o Iconify entrega por URL e o Edge já existe na máquina.
