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
| `YOUTUBE_LOGIN` | | `false` | Pede o login do YouTube no console durante o boot.<br>Só faz sentido em servidor — veja [Login do YouTube](#login-do-youtube-só-em-servidor) |

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

Sobe o bot e o Lavalink juntos, já ligados pela rede interna do compose:

```bash
docker compose up -d --build
docker compose logs -f alephbot
```

O `compose.yaml` injeta `LAVALINK_URI` e `LAVALINK_PASSWORD` nos dois contêineres, então o
que estiver no `Config/.env` para essas duas chaves é ignorado — trocar a senha do Lavalink
é mexer em `LAVALINK_PASSWORD` no `.env` da raiz do projeto.

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

Depois que você autoriza, o refresh token sai no mesmo console, já no formato do arquivo.
Guarde no `.env` da **raiz** (o que o compose lê), recrie o áudio e desligue o `YOUTUBE_LOGIN`:

```bash
YOUTUBE_REFRESH_TOKEN=1//0e...
```

```bash
docker compose up -d lavalink
```

> [!NOTE]
> Use uma **conta Google descartável**: o padrão de acesso de um bot pode fazer o YouTube
> sinalizar a conta. Em casa, com IP residencial, nada disso é necessário — a variável fica
> vazia e o plugin nem pede login.

<br>

### 💻 Local

Precisa de um Lavalink de pé. O `Lavalink/application.yml` do repositório já está
configurado com os padrões que o bot espera:

```bash
# em um terminal, com o application.yml deste repositório ao lado do jar
java -jar Lavalink.jar

# em outro
dotnet run
```

<br>

---

## Estrutura

```
Config/        Carregamento do .env e o objeto de configuração
Core/
Commands/      Um arquivo por comando, agrupados por categoria
Personality/   Todo texto que o usuário lê — mudar o tom do bot é mexer só aqui
Threnodian/    Bootstrap: host, DI, logging, gateway, serviços de fundo
Lavalink/      application.yml do servidor de áudio
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
