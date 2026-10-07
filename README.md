<div align="center">

# AlephBot

**Bot de Discord em C# com moderação, música e uma API de status.**


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

### Anime

| | Comando | O que faz |
|:--:|:--|:--|
| 📺 | **`/ma anime <nome>`** <sub>· `mal` · `myanimelist`</sub> | Ficha do anime no MyAnimeList |
| 📖 | **`/ma manga <nome>`** <sub>· `mal` · `myanimelist`</sub> | Ficha do mangá no MyAnimeList |

A ficha vem em embed com a capa, a nota do MAL e quantos votos ela tem, ranking, formato,
situação, período, estúdio (ou autor), gêneros e a sinopse inteira — traduzida pro português
na hora (via Google), com aviso quando o tradutor não respondeu e ela ficou em inglês. No modo
texto o tipo é a primeira palavra: `!ma anime Naruto`, `!ma mangá Berserk`.

Sem configurar nada os dados vêm do [Jikan](https://jikan.moe), a API não-oficial do MAL, que
cai junto com o humor do site. Com um `MAL_CLIENT_ID` no `.env` a busca vai pela
[API oficial](https://myanimelist.net/apiconfig) — veja [Configuração](#configuração).

<br>

### Geral

| | Comando | O que faz |
|:--:|:--|:--|
| 📖 | **`/help`** <sub>· `ajuda` · `comandos`</sub> | Lista o que o bot sabe fazer |
| 📡 | **`/ping`** | Latência do gateway |

<br>

---

## API

Com uma `API_KEY` no `.env` o bot sobe junto uma API HTTP — no mesmo processo, lendo direto
do gateway, dos players e da lista de comandos. Só leitura: status e estatísticas. Sem a
chave ela não existe, e nenhuma porta abre.

Pra ligar:

1. Gere uma chave com `openssl rand -hex 32` e ponha no `Config/.env`: `API_KEY=<a chave>`.
2. Suba o bot de novo — `docker compose up -d`, ou `dotnet run` fora do Docker. O boot avisa
   `API ligada` no log.
3. Confira com o `/api/health`, que não pede chave: `curl http://localhost:8080/api/health`
   devolve `{"status":"ok","discord":"connected","lavalink":"connected"}`.

| | Rota | O que devolve |
|:--:|:--|:--|
| 💓 | **`GET /api/health`** | `ok`, `degraded` (responde comando, mas sem música) ou `down` (sem Discord, com **503**).<br>A única rota sem chave — é a que monitor de uptime chama (aceita `HEAD` também) |
| 🪪 | **`GET /api/status`** | Quem o bot é, versão, uptime, latência do gateway e o estado do Lavalink |
| 📊 | **`GET /api/stats`** | Servidores, membros, pessoas online, players de música e memória |
| 📜 | **`GET /api/commands`** | Os comandos do `/help`, com a forma em barra, a de prefixo e os atalhos |

Todo pedido, menos o `/api/health`, leva a chave no header `X-Api-Key`:

```bash
curl -H "X-Api-Key: $API_KEY" http://localhost:8080/api/status
```

```json
{
  "bot": { "id": "123456789012345678", "username": "AlephBot", "avatarUrl": "https://cdn.discordapp.com/avatars/..." },
  "version": "1.0.0",
  "runtime": ".NET 10.0.12",
  "mode": "production",
  "prefix": "!",
  "startedAt": "2026-10-07T12:48:42.53+00:00",
  "uptimeSeconds": 3600,
  "discord": { "status": "connected", "latencyMs": 42 },
  "lavalink": {
    "status": "connected",
    "since": "2026-10-07T12:48:50.11+00:00",
    "stats": { "players": 1, "playingPlayers": 1, "uptimeSeconds": 3590, "memoryUsedMb": 180.2, "cpuLoad": 0.012, "reportedAt": "2026-10-07T13:48:20.4+00:00" }
  }
}
```

```bash
curl -H "X-Api-Key: $API_KEY" http://localhost:8080/api/stats
```

```json
{
  "guilds": 12,
  "members": 3456,
  "online": 210,
  "commands": 21,
  "music": { "players": 2, "playing": 1, "paused": 1, "queuedTracks": 17 },
  "memory": { "workingSetMb": 142.7, "gcHeapMb": 38.1 }
}
```

```bash
curl -H "X-Api-Key: $API_KEY" http://localhost:8080/api/commands
```

```json
[
  {
    "name": "play",
    "description": "Toca uma música ou põe ela na fila.",
    "category": "music",
    "slash": "/play <nome ou link>",
    "text": "!play <nome ou link>",
    "aliases": ["p", "tocar"]
  }
]
```

- IDs do Discord saem como texto: são inteiros de 64 bits, e o JavaScript arredonda número desse tamanho.
- `members` soma os membros de cada servidor (quem está em dois conta duas vezes); `online` são
  pessoas distintas, sem bots — o mesmo número da presença do bot.
- `lavalink.stats` é o último relatório do próprio Lavalink, que chega a cada minuto: `null`
  até o primeiro, e de novo depois de uma queda.
- `/api/commands` traz um item por comando, em ordem alfabética (acima, só o `/play`). O `text`
  já vem com o `PREFIX` do `.env`, e `slash` ou `text` vêm `null` quando o comando só existe de
  um jeito.
- Erro sai como `application/problem+json`: **401** sem chave ou com a chave errada, **404**,
  **405**. Chave errada fica anotada no log, com o IP de quem tentou (atrás de um proxy, o IP
  que aparece é o do proxy).

> [!IMPORTANT]
> Fora do Docker a API só escuta em `localhost`. No compose ela é publicada no `127.0.0.1` da
> máquina — o HTTP leva a chave em texto puro, então pra abrir pra fora o certo é um proxy com
> HTTPS na frente (Caddy, nginx). `API_BIND=0.0.0.0` no `.env` da raiz abre direto, e o Docker
> publica por cima do firewall (`ufw`): abre mesmo.

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
| `MAL_CLIENT_ID` | | *vazio* | Client ID da [API oficial do MyAnimeList](https://myanimelist.net/apiconfig) para o `/ma`.<br>Vazio = usa o Jikan, sem chave. Para criar um: *Create ID*, App Type *other*, e copie o Client ID |
| `API_KEY` | | *vazio* | Chave da [API](#api). Vazio = API desligada, nenhuma porta aberta.<br>Mínimo de 16 caracteres — `openssl rand -hex 32` gera uma boa |
| `API_PORT` | | `8080` | Porta da API. Fora do Docker ela só escuta em `localhost`; no compose quem manda é o `.env` da raiz |

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
| `API_PORT` | `8080` | Porta da [API](#api) na máquina. Dentro do contêiner ela é sempre `8080` |
| `API_BIND` | `127.0.0.1` | Onde essa porta é publicada. `0.0.0.0` abre pra rede — leia o aviso da [API](#api) antes |

Três volumes sobrevivem ao `docker compose down`: `aleph-logs` (logs em arquivo),
`aleph-data` (o token do YouTube e o que cada servidor estava tocando) e `lavalink-plugins`
(o plugin baixado). `down -v` apaga os três — o login do YouTube tem que ser refeito.

> [!NOTE]
> A porta `2333` do Lavalink não é publicada: ele só existe dentro da rede do compose. A da
> [API](#api) é, mas só no `127.0.0.1` da máquina — e só responde com `API_KEY` no `Config/.env`.

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
  Api/            A API HTTP: rotas, a chave e o que ela lê do bot
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
