<div align="center">

# AlephBot

**Bot de Discord em C# com moderação e música.**


<br>

[![C#](https://img.shields.io/badge/C%23-af87ff?style=for-the-badge&logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![.NET 10](https://img.shields.io/badge/.NET%2010-8b5cf6?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download)
[![Discord](https://img.shields.io/badge/Discord-6d4aff?style=for-the-badge&logo=discord&logoColor=white)](https://discord.com/developers/applications)
[![Docker](https://img.shields.io/badge/Docker-5b3ecf?style=for-the-badge&logo=docker&logoColor=white)](https://docs.docker.com/compose/)

[![AlephBot v1.0.0](https://img.shields.io/badge/AlephBot-v1.0.0-af87ff?style=flat-square)](AlephBot.csproj)
[![NetCord](https://img.shields.io/badge/NetCord-1.0.0--beta.19-af87ff?style=flat-square)](https://netcord.dev)
[![Lavalink](https://img.shields.io/badge/Lavalink-4.2.2-af87ff?style=flat-square)](https://lavalink.dev)
[![License](https://img.shields.io/badge/license-MIT-af87ff?style=flat-square)](LICENSE)

</div>

<br>

## Comandos

Todos funcionam como **slash command** (`/ping`) e como **comando de texto** (`!ping`, com o
prefixo do `.env`). Os apelidos entre parênteses valem só no modo texto.

<br>

### Moderação

| | Comando | O que faz |
|:--:|:--|:--|
| 🔨 | **`/ban`** <sub>· `banir`</sub> | Bane um usuário do servidor |
| 🕊️ | **`/unban`** <sub>· `desbanir`</sub> | Remove o banimento |
| 👢 | **`/kick`** <sub>· `expulsar`</sub> | Expulsa um usuário |
| 🔇 | **`/mute`** <sub>· `mutar` · `silenciar` · `calar`</sub> | Silencia no texto e na voz |
| 🔉 | **`/unmute`** <sub>· `desmutar` · `dessilenciar`</sub> | Devolve a voz |

<br>

### Música

| | Comando | O que faz |
|:--:|:--|:--|
| ▶️ | **`/play`** <sub>· `p` · `tocar`</sub> | Toca uma faixa ou põe na fila |
| 📚 | **`/playlist add`** <sub>· `pl add`</sub> | Acha uma playlist do YouTube pelo nome (ou link) e põe inteira na fila |
| ⏭️ | **`/skip`** <sub>· `s` · `pular` · `next`</sub> | Pula a faixa atual |
| ⏸️ | **`/pause`** <sub>· `pausar`</sub> | Pausa a reprodução |
| ⏯️ | **`/resume`** <sub>· `voltar` · `continuar`</sub> | Retoma de onde parou |
| ⏹️ | **`/stop`** <sub>· `parar`</sub> | Para tudo e limpa a fila |
| 👋 | **`/disconnect`** <sub>· `leave` · `sair` · `dc`</sub> | Sai do canal de voz |
| 📜 | **`/queue`** <sub>· `q` · `fila`</sub> | Mostra a fila |
| 💿 | **`/nowplaying`** <sub>· `np` · `agora` · `tocando`</sub> | Faixa atual e progresso |
| 🔀 | **`/shuffle`** <sub>· `embaralhar`</sub> | Embaralha a fila |
| 🔁 | **`/loop`** <sub>· `repeat` · `repetir`</sub> | Repete a faixa, a fila, ou nada |
| ⏩ | **`/seek`** <sub>· `ir` · `pular-para`</sub> | Anda para um ponto da faixa |
| 🔊 | **`/volume`** <sub>· `vol` · `v`</sub> | Mostra ou muda o volume |

Restart no meio da música não perde nada: o bot guarda o que cada servidor estava tocando
(faixa, posição, fila, volume, loop) no volume `aleph-data` e, ao voltar, entra de novo no
canal e continua de onde parou — desde que ainda tenha alguém lá e faça menos de 30 minutos.

<br>

### Geral

| | Comando | O que faz |
|:--:|:--|:--|
| 📖 | **`/help`** <sub>· `ajuda` · `comandos`</sub> | Lista o que o bot sabe fazer |
| 📡 | **`/ping`** | Latência do gateway |

<br>

---

## Configuração

Toda a configuração vem de variáveis de ambiente. Em desenvolvimento elas são lidas de
`Config/.env`; em produção, do próprio ambiente do contêiner.

```bash
cp Config/.env.example Config/.env
```

Depois preencha o `TOKEN`:

| Variável | | Padrão | Descrição |
|:--|:--:|:--|:--|
| **`TOKEN`** | ✅ | — | Token do bot no [Discord Developer Portal](https://discord.com/developers/applications) |
| `PREFIX` | | `!` | Prefixo dos comandos de texto |
| `DEV_GUILD_ID` | | *vazio* | ID do servidor de testes: registra os slash commands nele na hora.<br>Vazio = registro global (~1h para propagar) |
| `LOG_LEVEL` | | `Information` | `Trace` · `Debug` · `Information` · `Warning` · `Error` · `Critical` |
| `LAVALINK_URI` | | `http://localhost:2333/` | Endereço REST do servidor Lavalink |
| `LAVALINK_PASSWORD` | | `youshallnotpass` | Senha do Lavalink |
| `MUSIC_IDLE_MINUTES` | | `2` | Minutos parado (canal vazio ou nada tocando) antes de sair da voz |
| `YOUTUBE_LOGIN` | | `false` | Pede o login do YouTube no console quando não há token guardado.<br>Só faz sentido em servidor — veja [Login do YouTube](#login-do-youtube-só-em-servidor) |

<sub>✅ = obrigatória</sub>

> [!IMPORTANT]
> `Config/.env` está no `.gitignore` e no `.dockerignore` — ele nunca entra no repositório
> nem na imagem. Se o bot subir sem `TOKEN`, ele cria o arquivo a partir do template e para
> com a instrução na tela.

<br>

### Intents privilegiadas

No Developer Portal, aba **Bot**, ligue:

- **Message Content Intent** — sem ela os comandos de texto não chegam
- **Server Members Intent**
- **Presence Intent** — usada para contar quem está online

<br>

---

## Rodando

### 🐳 Docker <sub>recomendado</sub>

Sobe três contêineres na rede interna do compose: o bot, o Lavalink (áudio) e o
`yt-cipher`, que decifra as assinaturas do player do YouTube — o decifrador embutido no
plugin quebra a cada mudança do YouTube; esse acompanha.

```bash
docker compose up -d --build
docker compose logs -f alephbot
```

O bot só é iniciado depois que o Lavalink responde ao healthcheck, então o primeiro `up`
pode levar um minuto: o plugin do YouTube é baixado antes do servidor abrir a porta.

O `compose.yaml` injeta `LAVALINK_URI` e `LAVALINK_PASSWORD` nos contêineres, então o que
estiver no `Config/.env` para essas duas chaves é ignorado. O que o compose lê é o `.env`
da **raiz** do projeto — um arquivo diferente do `Config/.env`:

| Variável | Padrão | Descrição |
|:--|:--|:--|
| `LAVALINK_PASSWORD` | `youshallnotpass` | Senha do Lavalink, nos dois lados |
| `YOUTUBE_REFRESH_TOKEN` | *vazio* | Semente do login do YouTube — veja [abaixo](#login-do-youtube-só-em-servidor) |
| `YOUTUBE_PO_TOKEN`<br>`YOUTUBE_VISITOR_DATA` | *vazio* | Par que responde ao *"Sign in to confirm you're not a bot"* nos clients WEB.<br>Gere os dois com `docker run --rm quay.io/invidious/youtube-trusted-session-generator` |

Três volumes sobrevivem ao `docker compose down`: `aleph-logs` (logs em arquivo),
`aleph-data` (o token do YouTube e o que cada servidor estava tocando) e `lavalink-plugins`
(o plugin baixado). `down -v` apaga os três — o login do YouTube tem que ser refeito.

> [!NOTE]
> A porta `2333` do Lavalink não é publicada: ele só existe dentro da rede do compose.

<br>

#### Login do YouTube <sub>só em servidor</sub>

De um IP de datacenter (EC2, DigitalOcean e afins) o YouTube responde *"This video requires
login"* mesmo em vídeo público: a busca funciona, o áudio não. Quem usa o token é o Lavalink,
mas quem pede o login é o bot — assim o código aparece no console dele, junto do resto.

Ligue `YOUTUBE_LOGIN=true` no `Config/.env`, suba e acompanhe:

```bash
docker compose up -d --build
docker compose logs -f alephbot
```

Ela pede o código na saudação dela:

```
  Denia · login do YouTube
  O YouTube não acredita que um servidor escuta música. Faz o login que eu espero.

  1. abre https://www.google.com/device
  2. entra com uma conta descartável (não a sua principal)
  3. digita o código ABC-DEF-GHI
```

Depois que você autoriza, acabou: o bot guarda o refresh token no volume `aleph-data`
(`/app/data/youtube-token.json`) e entrega ele ao Lavalink toda vez que os dois se
conectam — boot, queda de rede ou restart só do áudio. Nada de colar em arquivo nem recriar
container.

O token não tem prazo, mas o Google invalida quando cisma com a conta. Quando isso
acontece o Lavalink recusa a entrega e o bot pede um login novo sozinho, no mesmo log —
digite o código e a música volta, sem restart. Se o Google trocar o token por um novo
durante uma renovação, o bot guarda o novo ao desligar.

Já tem um token de antes? Ponha em `YOUTUBE_REFRESH_TOKEN` no `.env` da **raiz** (o que o
compose lê): ele é a semente do primeiro boot, e também vale quando você cola um token
diferente ali (trocar de conta, por exemplo). Fora isso a variável pode ficar vazia.

> [!NOTE]
> Use uma **conta Google descartável**: o padrão de acesso de um bot pode fazer o YouTube
> sinalizar a conta — e é isso, não o tempo, que mata o token. Em casa, com IP residencial,
> nada disso é necessário — sem `YOUTUBE_LOGIN` e sem token, o bot nem toca no assunto.

<br>

### 💻 Local

Precisa de um Lavalink de pé. O `Lavalink/application.yml` do repositório já está
configurado com os padrões que o bot espera, com uma ressalva: ele aponta o decifrador de
assinaturas para `http://yt-cipher:8001`, um nome que só existe na rede do compose. Fora
dele, suba o serviço à parte e troque o `remoteCipher.url` para `http://localhost:8001`:

```bash
# o decifrador, em um terminal
docker run --rm -p 8001:8001 -e OVERRIDE_PLAYER_VARIANT=IAS ghcr.io/kikkia/yt-cipher:master

# o Lavalink em outro, com o application.yml deste repositório ao lado do jar
java -jar Lavalink.jar

# e o bot num terceiro
dotnet run
```

<br>

---

## Estrutura

```
Config/           Carregamento do .env e o objeto de configuração
Core/
  Commands/       Um arquivo por comando, agrupados por categoria
  Personality/    Todo texto que o usuário lê — mudar o tom do bot é mexer só aqui
Threnodian/       Bootstrap: host, DI, logging, gateway e os serviços de fundo
  Youtube/        Login (o token guardado e entregue ao Lavalink) e a busca de playlist
  Players/        A foto de cada player e a volta depois de um restart
Lavalink/         application.yml do servidor de áudio
```

> [!TIP]
> Comandos são descobertos por reflection: qualquer classe que implemente `ICommand` entra
> no `/help` sozinha, sem registro manual em lugar nenhum.

<br>

## Requisitos

| | |
|:--|:--|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | obrigatório |
| [Docker](https://docs.docker.com/get-docker/) | opcional — para o compose |
| Java 17+ | só para rodar o Lavalink na mão |

<br>

---

<div align="center">
<sub>Distribuído sob a licença <a href="LICENSE">MIT</a>.</sub>
</div>
