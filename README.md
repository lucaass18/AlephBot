# AlephBot

Bot de Discord em C# / .NET 10, com moderação e música. A voz dele é a **Denia**
(*Wuthering Waves*) — niilista de bom humor, sonolenta e cúmplice.

Construído sobre [NetCord](https://netcord.dev) para o gateway e
[Lavalink4NET](https://github.com/angelobreuer/Lavalink4NET) para o áudio.

---

## Comandos

Todos funcionam como **slash command** (`/ping`) e como **comando de texto**
(`!ping`, com o prefixo do `.env`). Os apelidos entre parênteses valem só no modo texto.

### Moderação

| Comando | O que faz |
| --- | --- |
| `/ban` (`banir`) | Bane um usuário do servidor |
| `/unban` (`desbanir`) | Remove o banimento |
| `/kick` (`expulsar`) | Expulsa um usuário |
| `/mute` (`mutar`, `silenciar`, `calar`) | Silencia no texto e na voz |
| `/unmute` (`desmutar`, `dessilenciar`) | Devolve a voz |

### Música

| Comando | O que faz |
| --- | --- |
| `/play` (`p`, `tocar`) | Toca uma faixa ou põe na fila |
| `/skip` (`s`, `pular`, `next`) | Pula a faixa atual |
| `/pause` (`pausar`) · `/resume` (`voltar`) | Pausa e retoma |
| `/stop` (`parar`) | Para tudo e limpa a fila |
| `/disconnect` (`leave`, `sair`, `dc`) | Sai do canal de voz |
| `/queue` (`q`, `fila`) | Mostra a fila |
| `/nowplaying` (`np`, `agora`) | Faixa atual e progresso |
| `/shuffle` (`embaralhar`) | Embaralha a fila |
| `/loop` (`repeat`, `repetir`) | Repete a faixa, a fila, ou nada |
| `/seek` (`ir`) | Anda para um ponto da faixa |
| `/volume` (`vol`, `v`) | Mostra ou muda o volume |

### Geral

| Comando | O que faz |
| --- | --- |
| `/help` (`ajuda`, `comandos`) | Lista o que o bot sabe fazer |
| `/ping` | Latência do gateway |

---

## Configuração

Toda a configuração vem de variáveis de ambiente. Em desenvolvimento elas são lidas
de `Config/.env`; em produção, do próprio ambiente do contêiner.

```bash
cp Config/.env.example Config/.env
```

Depois preencha o `TOKEN`:

| Variável | Obrigatória | Padrão | Descrição |
| --- | --- | --- | --- |
| `TOKEN` | **sim** | — | Token do bot no [Discord Developer Portal](https://discord.com/developers/applications) |
| `PREFIX` | não | `!` | Prefixo dos comandos de texto |
| `DEV_GUILD_ID` | não | *(vazio)* | ID do servidor de testes: registra os slash commands nele na hora. Vazio = registro global (~1h para propagar) |
| `LOG_LEVEL` | não | `Information` | `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical` |
| `LAVALINK_URI` | não | `http://localhost:2333/` | Endereço REST do servidor Lavalink |
| `LAVALINK_PASSWORD` | não | `youshallnotpass` | Senha do Lavalink |
| `MUSIC_IDLE_MINUTES` | não | `2` | Minutos parado (canal vazio ou nada tocando) antes de sair da voz |

> `Config/.env` está no `.gitignore` e no `.dockerignore` — ele nunca entra no
> repositório nem na imagem. Se o bot subir sem `TOKEN`, ele cria o arquivo a partir
> do template e para com a instrução na tela.

### Intents privilegiadas

No Developer Portal, aba **Bot**, ligue:

- **Message Content Intent** — sem ela os comandos de texto não chegam
- **Server Members Intent**
- **Presence Intent** — usada para contar quem está online

---

## Rodando

### Docker (recomendado)

Sobe o bot e o Lavalink juntos, já ligados pela rede interna do compose:

```bash
docker compose up -d --build
docker compose logs -f alephbot
```

O `compose.yaml` injeta `LAVALINK_URI` e `LAVALINK_PASSWORD` nos dois contêineres, então
o que estiver no `Config/.env` para essas duas chaves é ignorado — trocar a senha do
Lavalink é mexer em `LAVALINK_PASSWORD` no `.env` da raiz do projeto.

A porta `2333` do Lavalink não é publicada: ele só existe dentro da rede do compose.

### Local

Precisa de um Lavalink de pé. O `Lavalink/application.yml` do repositório já está
configurado com os padrões que o bot espera:

```bash
# em um terminal, com o application.yml deste repositório ao lado do jar
java -jar Lavalink.jar

# em outro
dotnet run
```

---

## Estrutura

```
Config/          Carregamento do .env e o objeto de configuração
Core/
  Commands/      Um arquivo por comando, agrupados por categoria
  Personality/   Todo texto que o usuário lê — mudar o tom do bot é mexer só aqui
Threnodian/      Bootstrap: host, DI, logging, gateway, serviços de fundo
Lavalink/        application.yml do servidor de áudio
```

Comandos são descobertos por reflection: qualquer classe que implemente `ICommand`
entra no `/help` sozinha, sem registro manual em lugar nenhum.

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker (opcional, para o compose)
- Java 17+ (só se for rodar o Lavalink na mão)
