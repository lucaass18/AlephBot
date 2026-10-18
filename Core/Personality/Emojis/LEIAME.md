# Os emoji da fala

Emoji de reação, pra Denia usar dentro de uma frase. São o par da pasta
`Core/Commands/Anime/Emojis/`, que é a dos ícones de rótulo da ficha — e as duas servem a
coisas opostas.

O bot sobe estes sozinho no boot, junto dos outros: qualquer `.png` numa pasta chamada
`Emojis` entra, e o nome do arquivo vira o nome do emoji.

## Não servem como ícone de rótulo

São ilustrações de corpo inteiro, desenhadas pra aparecer grandes. Nome de campo de embed
renderiza emoji a uns 22px, e nesse tamanho estes aqui viram borrão colorido — os de texto
japonês (`idol_jidan`, `idol_oshi`, `idol_suki`) somem por completo.

Onde eles funcionam é solto numa frase, onde o Discord desenha emoji maior, e principalmente
numa mensagem que só tem emoji — aí o Discord renderiza no tamanho grande.

Para rótulo, os de `Anime/Emojis/`: traço simples, uma cor só, legíveis pequenos.

## De onde vieram

Dois pacotes da comunidade do [emoji.gg](https://emoji.gg):

| Pasta de origem | Arquivos aqui |
|---|---|
| `312234-red-idol-pack` | `idol_jidan`, `idol_laco`, `idol_oshi`, `idol_suki`, `idol_varinha` |
| `825234-pastel-pack-v2` | `pastel_abraco`, `pastel_adora`, `pastel_himitsu`, `pastel_look5`, `pastel_look6`, `pastel_segredo`, `pastel_surta`, `pastel_torce` |

Os nomes originais vinham com o id do emoji.gg na frente e hífen no meio
(`196031-idolredjidan.png`), e o Discord só aceita letra, número e sublinhado — por isso foram
renomeados. Quem subiu lá define a licença de cada um, item a item; não é arte original do
projeto, e vale saber disso se um dia algum sumir do app por pedido de remoção. Sumindo, o
lugar que usava aquele emoji cai no texto de reserva.
