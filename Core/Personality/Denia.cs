using System.Globalization;

using AlephBot.Core.Commands.Interface;
using AlephBot.Threnodian.MyAnimeList;

namespace AlephBot.Core.Personality;

/// <summary>
/// A Denia (Wuthering Waves): niilista de bom humor, sonolenta, gulosa e cúmplice. Ela
/// alfineta, mas nunca esconde a informação atrás da piada — toda frase daqui ainda diz
/// o que aconteceu e o que fazer.
///
/// Todo texto que o usuário lê sai deste arquivo. Mudar o tom dela é mexer só aqui.
/// </summary>
public static class Denia
{
    /// <summary>A fala de saudação dela; serve de assinatura nos rodapés.</summary>
    public const string Assinatura = "Shh... fica entre a gente.";

    // ---- ping ----------------------------------------------------------------

    public static string PingRápido() => Pick(
        "Acordada. Surpresa?",
        "Rápida assim? Aproveita, não é sempre.",
        "Tô aqui. Infelizmente.");

    public static string PingNormal() => Pick(
        "Dá pro gasto.",
        "Nem rápida, nem lenta. Como quase tudo.",
        "Tô no automático, mas respondo.");

    public static string PingLento() => Pick(
        "Tá arrastado... igual eu de manhã.",
        "Devagar. Me deixa dormir mais um pouco.",
        "Isso aqui tá agonizando, sinceramente.");

    // ---- moderação: embed ----------------------------------------------------

    public static string Título(AçãoModeração ação) => ação switch
    {
        AçãoModeração.Kick => "Bolha estourada",
        AçãoModeração.Mute => "Silêncio, por favor",
        AçãoModeração.Unmute => "Voz devolvida",
        AçãoModeração.Unban => "Cinzas remontadas",
        _ => "Cinzas",
    };

    public static string Descrição(AçãoModeração ação, string alvo, ulong alvoId) => ação switch
    {
        AçãoModeração.Kick => Pick(
            $"{alvo} (`{alvoId}`) foi devolvido pro Vazio.",
            $"{alvo} (`{alvoId}`) sumiu. Pedaço por pedaço.",
            $"Estourei a bolha de {alvo} (`{alvoId}`). Tchau."),

        AçãoModeração.Mute => Pick(
            $"{alvo} (`{alvoId}`) não fala mais. Nem digita, nem abre o microfone.",
            $"Coloquei {alvo} (`{alvoId}`) no mudo. Texto e voz, pacote completo.",
            $"{alvo} (`{alvoId}`) vai ficar quietinho por um tempo."),

        AçãoModeração.Unmute => Pick(
            $"{alvo} (`{alvoId}`) pode falar de novo. Espero que o silêncio tenha ensinado alguma coisa.",
            $"Devolvi a voz de {alvo} (`{alvoId}`). Usa com moderação.",
            $"{alvo} (`{alvoId}`) voltou a falar. A paz durou pouco."),

        AçãoModeração.Unban => Pick(
            $"{alvo} (`{alvoId}`) juntou as próprias cinzas e voltou.",
            $"Tirei {alvo} (`{alvoId}`) da lista negra. Segunda chance é coisa rara por aqui.",
            $"{alvo} (`{alvoId}`) pode voltar. O Vazio cuspiu de volta, aparentemente."),

        _ => Pick(
            $"{alvo} (`{alvoId}`) virou cinzas. E cinzas não voltam.",
            $"{alvo} (`{alvoId}`) foi riscado do mapa. Permanente.",
            $"Acabou pra {alvo} (`{alvoId}`). Escuridão. Fim."),
    };

    public static string CampoAlvo(AçãoModeração ação) => ação switch
    {
        AçãoModeração.Kick => "👤 Quem sumiu",
        AçãoModeração.Mute => "👤 Quem foi calado",
        AçãoModeração.Unmute => "👤 Quem recuperou a voz",
        AçãoModeração.Unban => "👤 Quem voltou das cinzas",
        _ => "👤 Quem virou cinzas",
    };

    public static string CampoModerador(AçãoModeração ação) => ação switch
    {
        AçãoModeração.Kick => "🛡️ Quem mandou sumir",
        AçãoModeração.Mute => "🛡️ Quem mandou calar",
        AçãoModeração.Unmute => "🛡️ Quem devolveu a voz",
        AçãoModeração.Unban => "🛡️ Quem teve pena",
        _ => "🛡️ Quem riscou do mapa",
    };

    public const string CampoMotivo = "📝 Motivo";

    public static string CampoExtra(AçãoModeração ação) => ação switch
    {
        AçãoModeração.Mute => "⏳ Volta a falar",
        AçãoModeração.Ban => "🧹 Mensagens apagadas",
        AçãoModeração.Unmute => "⏳ Silêncio que sobrava",
        AçãoModeração.Unban => "🧾 Tinha sido banido por",
        _ => "ℹ️ Detalhe",
    };

    public static string Rodapé(string moderador) =>
        $"{Assinatura} · executado por {moderador}";

    // ---- moderação: DM pro punido --------------------------------------------

    public static string DmTítulo(AçãoModeração ação) => ação switch
    {
        AçãoModeração.Kick => "👢 Você foi expulso",
        AçãoModeração.Mute => "🤫 Você foi silenciado",
        AçãoModeração.Unmute => "🔊 Você pode falar de novo",
        AçãoModeração.Unban => "🕊️ Seu banimento foi removido",
        _ => "🔨 Você foi banido",
    };

    public static string DmDescrição(AçãoModeração ação, string servidor) => ação switch
    {
        AçãoModeração.Kick => $"Você foi expulso de **{servidor}**. Nada pessoal — quase nada é.",
        AçãoModeração.Mute => $"Você levou silêncio em **{servidor}**, no texto e na voz. Aproveita pra dormir.",
        AçãoModeração.Unmute => $"Seu silêncio em **{servidor}** acabou mais cedo. Não me faz arrepender.",
        AçãoModeração.Unban => $"Seu banimento em **{servidor}** foi removido. Não abusa da sorte.",
        _ => $"Você foi banido de **{servidor}**. Esse aqui não tem volta.",
    };

    // ---- moderação: recusas --------------------------------------------------

    public static string RecusaPróprio(AçãoModeração ação) => ação switch
    {
        AçãoModeração.Kick => "Se expulsar sozinho? Que dramático. Não.",
        AçãoModeração.Mute => "Auto-silêncio? Se quer sossego, é só parar de digitar.",
        AçãoModeração.Unmute => "Você não tá calado. Se tivesse, nem conseguiria me pedir isso.",
        AçãoModeração.Unban => "Você não tá banido. Óbvio — acabou de falar comigo.",
        _ => "Se banir? Que dedicação ao drama. Não.",
    };

    public static string RecusaBot(AçãoModeração ação) => ação switch
    {
        AçãoModeração.Kick => "Me expulsar? Eu já moro no Vazio, obrigada. Usa o botão de sair do servidor.",
        AçãoModeração.Mute => "Me calar? Boa tentativa. Tira minha permissão se quiser silêncio.",
        AçãoModeração.Unmute => "Eu não tô calada. Infelizmente pra você.",
        AçãoModeração.Unban => "Eu não tô banida. Que ideia estranha.",
        _ => "Me banir? Nem em sonho. Tira minha permissão se te incomodo tanto.",
    };

    public static string RecusaDono() =>
        "O dono do servidor? Nem eu tenho essa coragem.";

    public static string HierarquiaInsuficiente(string alvo) =>
        $"{alvo} tem cargo igual ou acima do seu. Sonhar é de graça, punir não.";

    public static string Falhou(AçãoModeração ação, string alvo, string status)
    {
        var verbo = ação switch
        {
            AçãoModeração.Kick => "expulsar",
            AçãoModeração.Mute => "silenciar",
            AçãoModeração.Unmute => "devolver a voz de",
            AçãoModeração.Unban => "desbanir",
            _ => "banir",
        };

        return $"Não consegui {verbo} {alvo}. O Discord respondeu `{status}` — confere se meu cargo tá acima do dele.";
    }

    public static string SemMotivo() => "Nenhum motivo informado";

    // ---- moderação: entradas inválidas ---------------------------------------

    public static string DuraçãoInválida(string exemplo) =>
        $"Não entendi essa duração. Usa algo tipo `{exemplo}` — `30s`, `10m`, `2h`, `7d`.";

    public static string DuraçãoLongaDemais(int maxDias) =>
        $"O Discord não deixa silenciar por mais de {maxDias} dias. Pra mais que isso, é ban mesmo.";

    public static string ApagarInválido() =>
        "Dá pra apagar de 0 a 7 dias de mensagens. Fora disso o Discord não aceita.";

    public static string IdInválido() =>
        "Isso não parece um ID. Liga o Modo Desenvolvedor no Discord e usa `Copiar ID` — banido não dá pra mencionar.";

    public static string NãoEstáBanido(string alvo) =>
        $"{alvo} não está na lista de banidos. Ou já voltou, ou nunca saiu.";

    public static string NãoEstáSilenciado(string alvo) =>
        $"{alvo} não está calado. Não dá pra devolver o que eu não tirei.";

    // ---- help ----------------------------------------------------------------

    public const string HelpTítulo = "📖 O que eu sei fazer";

    public static string HelpIntro(int total, string prefixo) => Pick(
        $"{total} comandos. Todos atendem por `/` ou por `{prefixo}` — usa o que der menos trabalho.",
        $"Tenho {total} comandos aqui. Funciona com `/` e com `{prefixo}`, tanto faz.",
        $"São {total}. Decorei todos contra a minha vontade. `/` ou `{prefixo}`, como preferir.");

    public static string HelpDica() =>
        "`/help <comando>` se quiser um de perto.";

    public static string HelpCategoria(CommandCategory categoria) => categoria switch
    {
        CommandCategory.Moderation => "🔨 Moderação",
        CommandCategory.Utility => "🔧 Utilidades",
        CommandCategory.Music => "🎧 Música",
        CommandCategory.Anime => "🎌 Anime",
        CommandCategory.Fun => "🎲 Distração",
        CommandCategory.Owner => "👑 Só pro dono",
        _ => "🫧 Geral",
    };

    public static string HelpDetalheTítulo(string comando) => $"📖 {comando}";

    public const string HelpCampoUso = "⌨️ Como chamar";
    public const string HelpCampoCategoria = "📂 Categoria";
    public const string HelpCampoAtalhos = "🔁 Também atende por";

    public static string HelpNãoAchei(string nome) => Pick(
        $"Não tenho nada chamado `{nome}`. Confere no `/help` o que existe.",
        $"`{nome}`? Nunca ouvi falar. A lista inteira tá no `/help`.");

    public static string HelpVazio() =>
        "Nenhum comando registrado. Constrangedor, mas é o que tem.";

    public static string HelpRodapé() =>
        $"{Assinatura} · /help <comando> pra ver um de perto";

    // ---- música: títulos e rótulos -------------------------------------------

    public const string MúsicaTítuloTocando = "🎧 Tocando agora";
    public const string MúsicaTítuloNaFila = "➕ Entrou na fila";
    public const string MúsicaTítuloPlaylist = "📚 Playlist na fila";
    public const string MúsicaTítuloFila = "📋 A fila";
    public const string MúsicaTítuloVoltei = "🔌 De volta";

    public const string MúsicaCampoArtista = "🎤 Artista";
    public const string MúsicaCampoDuração = "⏱️ Duração";
    public const string MúsicaCampoPedidoPor = "🙋 Pedido por";
    public const string MúsicaCampoPosição = "🔢 Posição";
    public const string MúsicaCampoFonte = "📡 Fonte";
    public const string MúsicaCampoVolume = "🔊 Volume";
    public const string MúsicaCampoRepetição = "🔁 Repetição";
    public const string MúsicaCampoAgora = "▶️ Agora";
    public const string MúsicaCampoASeguir = "⏭️ A seguir";
    public const string MúsicaCampoTotal = "🧮 No total";

    /// <summary>Faixa sem fim: não tem barra de progresso nem duração pra mostrar.</summary>
    public const string MúsicaAoVivo = "🔴 ao vivo";

    public static string MúsicaRodapé(string quem) =>
        $"{Assinatura} · pedido por {quem}";

    // ---- música: recusas -----------------------------------------------------

    public static string MúsicaVocêForaDoCanal() => Pick(
        "Entra num canal de voz primeiro. Não vou cantar sozinha.",
        "Você não tá em canal nenhum. Difícil te fazer companhia assim.",
        "Canal de voz, por favor. Eu apareço depois de você.");

    public static string MúsicaCanalDiferente() => Pick(
        "Tô em outro canal. Vem pra cá, ou espera eu terminar.",
        "Você tá num canal, eu em outro. Um de nós dois tem que se mexer.",
        "Não atendo dois canais ao mesmo tempo. Escolhe o meu.");

    public static string MúsicaNãoEstouTocando() => Pick(
        "Não tô tocando nada. Silêncio é de graça.",
        "Não tem música nenhuma rolando. Usa `/play` se quiser mudar isso.",
        "Nada tocando. Tava tão bom o sossego.");

    public static string MúsicaFilaVazia() => Pick(
        "A fila tá vazia. Igual minha vontade de trabalhar.",
        "Não tem nada na fila. Usa `/play` e enche ela.",
        "Fila vazia. Aproveita o silêncio enquanto dura.");

    public static string MúsicaServidorFora() =>
        "O servidor de áudio não respondeu. Sem ele eu não toco nada — confere se o Lavalink tá de pé.";

    public static string MúsicaNãoAchei(string busca) => Pick(
        $"Não achei nada pra `{busca}`. Tenta com outras palavras, ou joga o link direto.",
        $"`{busca}` não me devolveu nada. Escreve diferente ou cola o link.");

    public static string MúsicaBuscaFalhou(string motivo) =>
        $"A busca falhou: {motivo}. Se for link de playlist privada ou vídeo com restrição, não tem jeito.";

    public static string MúsicaPlaylistNãoAchei(string busca) => Pick(
        $"Procurei playlist com `{busca}` e não veio nenhuma. Tenta outro nome, ou cola o link dela.",
        $"Nenhuma playlist pra `{busca}`. O YouTube tem quase tudo — quase.");

    public static string MúsicaPlaylistBuscaFalhou() =>
        "O YouTube não respondeu a busca de playlist. Tenta de novo daqui a pouco, ou cola o link direto.";

    public static string MúsicaNãoÉPlaylist() =>
        "Isso aí é um vídeo só, não uma playlist. Pra uma faixa, o `/play` resolve.";

    public static string MúsicaPlaylistAçãoDesconhecida(string ação, string uso) =>
        $"Não sei fazer `{ação}` com playlist. Por enquanto é só `{uso}`.";

    public static string MúsicaDesisti(int quantas, string motivo) =>
        $"{quantas} faixas seguidas falharam, então parei e limpei a fila — não vou anunciar uma por uma até acabar. O servidor de áudio disse: `{motivo}`";

    public static string MúsicaNãoConsegui() =>
        "Não consegui entrar no canal. Confere se eu tenho permissão de conectar e falar aí.";

    // ---- música: o que aconteceu ---------------------------------------------

    public static string MúsicaComeçou(string faixa) => Pick(
        $"Botei **{faixa}** pra tocar. Aproveita.",
        $"**{faixa}**, tocando. Não me peça pra dançar.",
        $"Tocando **{faixa}**. Eu ia dormir, mas tudo bem.");

    public static string MúsicaEntrouNaFila(string faixa, int posição) =>
        $"**{faixa}** entrou na fila, na posição **{posição}**.";

    public static string MúsicaPlaylistEntrou(string nome, int quantas) => Pick(
        $"Peguei **{quantas}** faixas de **{nome}**. Isso vai demorar.",
        $"**{quantas}** faixas de **{nome}** na fila. Espero que você tenha bom gosto.");

    /// <summary>
    /// Quando tem próxima, quem apresenta ela é o anúncio automático do player — por isso
    /// esta frase não repete o nome dela, senão o canal recebe a mesma informação duas vezes.
    /// </summary>
    public static string MúsicaPulou(string faixa, bool temPróxima) =>
        temPróxima
            ? $"Pulei **{faixa}**. Já ponho a próxima."
            : $"Pulei **{faixa}**. Não tem próxima — acabou aqui.";

    public static string MúsicaParou() => Pick(
        "Parei tudo e limpei a fila. Silêncio, finalmente.",
        "Acabou. Fila zerada, música morta.");

    public static string MúsicaSaiu() => Pick(
        "Saí do canal. Vou dormir.",
        "Tchau. Me chama de novo se der saudade.");

    public static string MúsicaPausou(string faixa) =>
        $"Pausei **{faixa}**. Continua com `/resume`.";

    public static string MúsicaJáPausada() =>
        "Já tá pausado. Você quis dizer `/resume`?";

    public static string MúsicaVoltou(string faixa) =>
        $"Voltando com **{faixa}**.";

    public static string MúsicaNãoEstavaPausada() =>
        "Não tá pausado. Tá tocando normal, escuta direito.";

    public static string MúsicaVolumeMudou(int porcento) => porcento switch
    {
        0 => "Volume no zero. É basicamente um mute com passos extras.",
        > 100 => $"Volume em **{porcento}%**. Depois não reclama do ouvido.",
        _ => $"Volume em **{porcento}%**.",
    };

    public static string MúsicaVolumeInválido(int max) =>
        $"O volume vai de 0 a {max}. Fora disso eu não vou.";

    public static string MúsicaLoopDesligado() =>
        "Repetição desligada. Cada faixa toca uma vez e morre em paz.";

    public static string MúsicaLoopFaixa(string faixa) =>
        $"Vou repetir **{faixa}** até você mandar parar. Sua sanidade, seu problema.";

    public static string MúsicaLoopFila() =>
        "A fila inteira agora roda em círculo. Sem fim, como quase tudo.";

    public static string MúsicaEmbaralhou(int quantas) =>
        $"Embaralhei as {quantas} faixas da fila. Boa sorte adivinhando a próxima.";

    public static string MúsicaFilaCurtaDemais() =>
        "Precisa de pelo menos duas faixas na fila pra embaralhar. Matemática básica.";

    public static string MúsicaPulouPara(string faixa, string posição) =>
        $"**{faixa}** agora em `{posição}`.";

    public static string MúsicaPosiçãoInválida(string exemplo) =>
        $"Não entendi essa posição. Usa `{exemplo}` — também aceito `90`, `1m30s` e `1:02:03`.";

    public static string MúsicaPosiçãoLongaDemais(string duração) =>
        $"Essa faixa tem só `{duração}`. Não dá pra pular pra depois do fim.";

    public static string MúsicaNãoDáProProcurar(string faixa) =>
        $"**{faixa}** não deixa avançar — transmissão ao vivo não tem onde chegar.";

    /// <summary>A despedida quando ninguém sobrou no canal ou nada tocou por tempo demais.</summary>
    public static string MúsicaInativa() => Pick(
        "Ficou todo mundo quieto, então eu saí. Estava mesmo com sono.",
        "Sem música e sem gente, não tenho o que fazer aqui. Saí.",
        "Cansei de esperar. Saí do canal.");

    public static string MúsicaFilaResumo(int faixas, string duração) =>
        $"{faixas} na fila · `{duração}` pela frente";

    public static string MúsicaFilaSobra(int quantas) =>
        $"…e mais {quantas}.";

    // ---- música: a volta depois de um restart --------------------------------

    public static string MúsicaVoltei(string faixa, string posição) => Pick(
        $"Reiniciei no meio de **{faixa}**. Voltei pra onde parou: `{posição}`.",
        $"Caí, levantei. **{faixa}** continua de `{posição}`.");

    public static string MúsicaVolteiDoComeço(string faixa) => Pick(
        $"Reiniciei e perdi o ponto de **{faixa}**. Vai do começo de novo.",
        $"Caí, levantei. **{faixa}** de novo, do começo — o ponto ficou pra trás.");

    public static string MúsicaVolteiPausada() =>
        "Estava em pausa; deixei como estava.";

    public static string MúsicaFilaVeioJunto(int quantas) =>
        quantas == 1
            ? "A fila veio junto: mais uma faixa."
            : $"A fila veio junto: mais {quantas} faixas.";

    // ---- login do YouTube ----------------------------------------------------
    //
    // sai no console: no primeiro boot com YOUTUBE_LOGIN ligado, e de novo se o Google
    // invalidar o token que eu guardava

    public const string YoutubeTítulo = "login do YouTube";

    public static string YoutubeAbertura() => Pick(
        "O YouTube não acredita que um servidor escuta música. Faz o login que eu espero.",
        "Daqui de dentro o YouTube me trata como robô. Ele não está errado, mas atrapalha.");

    public static string YoutubeEsperando() => Pick(
        "esperando você... sem pressa, eu ia cochilar mesmo",
        "esperando. Eu aviso quando ele liberar.");

    public static string YoutubePronto() => Pick(
        "Pronto. Guardei o token comigo — daqui em diante quem cuida dele sou eu.",
        "Deu certo. Já guardei o token; não precisa anotar nada.");

    public static string YoutubeOndeEstá(string caminho) =>
        $"ele fica em {caminho} — só precisa dele se for me levar pra outro servidor";

    public static string YoutubeTokenMorreu() => Pick(
        "O Google invalidou o token que eu tinha. Não fui eu — ele faz isso quando cisma com a conta. Vou pedir outro login:",
        "Meu token do YouTube morreu. O Google não avisa por quê; só peço de novo:");

    public static string YoutubeNegado() =>
        "Você recusou o acesso. Sem login, o YouTube continua fechado pra mim.";

    public static string YoutubeExpirou() =>
        "O código expirou esperando você. Na próxima conexão do Lavalink (ou se me subir de novo) eu peço outro.";

    public static string YoutubeFalhou(string motivo) =>
        $"O login não foi: {motivo}. Fica pro áudio o que não vem do YouTube.";

    // ---- MyAnimeList: a ficha ------------------------------------------------

    public static string MalTítulo(TipoDeObra tipo, string título) =>
        $"{(tipo == TipoDeObra.Anime ? "📺" : "📖")} {título}";

    public const string MalCampoNota = "⭐ Nota";
    public const string MalCampoRanking = "🏆 Ranking";
    public const string MalCampoSituação = "📡 Situação";
    public const string MalCampoGêneros = "🏷️ Gêneros";

    public static string MalCampoFormato(TipoDeObra tipo) =>
        tipo == TipoDeObra.Anime ? "📺 Formato" : "📖 Formato";

    public static string MalCampoPeríodo(TipoDeObra tipo) =>
        tipo == TipoDeObra.Anime ? "📅 Exibição" : "📅 Publicação";

    public static string MalCampoAutoria(TipoDeObra tipo) =>
        tipo == TipoDeObra.Anime ? "🎬 Estúdio" : "✍️ Autoria";

    /// <summary>Os outros nomes da obra, na linha de cima da sinopse. Eles já vêm em negrito.</summary>
    public static string MalTambémConhecida(string nomes) =>
        $"Também conhecido como {nomes}";

    public static string MalNota(double? nota, int? votos)
    {
        if (nota is not { } valor)
            return "Sem nota ainda. Ninguém se animou a votar.";

        // ponto e não vírgula: é a nota como o MAL mostra, e é assim que todo mundo cita ela
        var linha = $"**{valor.ToString("0.00", CultureInfo.InvariantCulture)}** / 10";

        return votos is > 0
            ? $"{linha}\n{votos.Value.ToString("N0", PtBr)} votos"
            : linha;
    }

    public static string MalRanking(int? posição, int? popularidade)
    {
        var nota = posição is { } p ? $"#{p.ToString("N0", PtBr)} em nota" : "Sem posição em nota";
        var gente = popularidade is { } g ? $"#{g.ToString("N0", PtBr)} em popularidade" : "Sem posição em popularidade";

        return $"{nota}\n{gente}";
    }

    public static string MalSituação(TipoDeObra tipo, Situação situação) => (tipo, situação) switch
    {
        (TipoDeObra.Anime, Situação.EmAndamento) => "Em exibição",
        (TipoDeObra.Manga, Situação.EmAndamento) => "Em publicação",
        (_, Situação.Concluída) => "Concluído",
        (TipoDeObra.Anime, Situação.NãoLançada) => "Ainda não estreou",
        (TipoDeObra.Manga, Situação.NãoLançada) => "Ainda não publicado",
        (_, Situação.EmHiato) => "Em hiato",
        (_, Situação.Cancelada) => "Cancelado",
        _ => "Ninguém sabe",
    };

    /// <summary>O formato do MAL ("tv", "movie", "light novel") em português; o que eu não conheço passa como veio.</summary>
    public static string? MalFormato(string? formato) => formato switch
    {
        null => null,
        "tv" => "TV",
        "movie" => "Filme",
        "ova" => "OVA",
        "ona" => "ONA",
        "special" => "Especial",
        "tv special" => "Especial de TV",
        "music" => "Clipe musical",
        "cm" => "Comercial",
        "pv" => "Trailer (PV)",
        "manga" => "Mangá",
        "novel" => "Novel",
        "light novel" => "Light novel",
        "one shot" => "One-shot",
        "doujinshi" => "Doujinshi",
        "manhwa" => "Manhwa",
        "manhua" => "Manhua",
        "oel" => "OEL (mangá ocidental)",
        "unknown" => null,
        _ => formato,
    };

    /// <summary>Quantos episódios, ou capítulos e volumes. O que o MAL ainda não sabe fica de fora.</summary>
    public static string? MalQuantidade(TipoDeObra tipo, int? episódios, int? capítulos, int? volumes)
    {
        if (tipo == TipoDeObra.Anime)
            return episódios is { } ep ? Plural(ep, "episódio") : null;

        var partes = new List<string>(2);

        if (capítulos is { } cap)
            partes.Add(Plural(cap, "capítulo"));

        if (volumes is { } vol)
            partes.Add(Plural(vol, "volume"));

        return partes.Count > 0 ? string.Join(" · ", partes) : null;
    }

    /// <summary>A classificação indicativa do MAL, do jeito que se lê aqui.</summary>
    public static string? MalClassificação(string? código) => código switch
    {
        "g" => "Livre",
        "pg" => "Infantil",
        "pg 13" => "13+",
        "r" => "17+",
        "r+" => "17+ (nudez leve)",
        "rx" => "18+ (hentai)",
        _ => null,
    };

    public static string MalPeríodo(DataParcial início, DataParcial fim, Situação situação)
    {
        if (início.ÉVazia)
            return "Sem data";

        if (situação == Situação.NãoLançada)
            return $"A partir de {início}";

        // sem fim porque ainda não acabou — diferente de filme, que tem um dia só
        if (fim.ÉVazia && situação == Situação.EmAndamento)
            return $"Desde {início}";

        // filme e one-shot têm um dia só; repetir ele dos dois lados não diz nada
        if (fim.ÉVazia || fim == início)
            return início.ToString();

        return $"{início} → {fim}";
    }

    /// <summary>O gênero do MAL em português. Os que o Brasil usa em japonês ou inglês ficam como estão.</summary>
    public static string MalGênero(string nome) =>
        Gêneros.TryGetValue(nome, out var traduzido) ? traduzido : nome;

    public static string MalSemSinopse() => Pick(
        "Sem sinopse no MAL. Ou ninguém escreveu, ou não tem o que contar.",
        "O MAL não tem sinopse pra esse. Vai ter que descobrir por conta própria.");

    /// <summary>Quando o tradutor não respondeu e a sinopse ficou como o MAL guarda: em inglês.</summary>
    public static string MalSinopseEmInglês() =>
        "*(o tradutor cochilou — a sinopse ficou em inglês)*";

    /// <summary>Sinopse maior que o embed aguenta: o resto fica no MAL, que o título já aponta.</summary>
    public static string MalSinopseCortada() =>
        "*(…continua no MyAnimeList — o título leva lá)*";

    /// <summary>Quem forneceu os dados e, quando houve tradução, o aviso de que foi máquina.</summary>
    public static string MalRodapé(string fonte, string quem, bool sinopseTraduzida) =>
        sinopseTraduzida
            ? $"{Assinatura} · dados do {fonte}, sinopse traduzida por máquina · pedido por {quem}"
            : $"{Assinatura} · dados do {fonte} · pedido por {quem}";

    // ---- MyAnimeList: recusas ------------------------------------------------

    public static string MalNãoAchei(TipoDeObra tipo, string busca)
    {
        var coisa = tipo == TipoDeObra.Anime ? "anime" : "mangá";

        return Pick(
            $"Procurei `{busca}` e o MAL não devolveu {coisa} nenhum. Confere a grafia, ou tenta o título original em romaji.",
            $"`{busca}`? Nenhum {coisa} com esse nome no MAL. Nem eu conheço, e olha que eu não durmo.");
    }

    public static string MalFora() => Pick(
        "O MyAnimeList não respondeu. Ele faz isso de vez em quando — tenta de novo daqui a pouco.",
        "O MAL tá fora do ar, ou fingindo que tá. Tenta mais tarde.");

    public static string MalCredencialRecusada() =>
        "O MyAnimeList recusou meu Client ID. Confere o `MAL_CLIENT_ID` no `.env` — ou apaga ele, que eu me viro pelo Jikan.";

    public static string MalTipoDesconhecido(string tipo, string uso) =>
        $"Não sei o que é `{tipo}`. É `anime` ou `mangá` — uso: `{uso}`";

    public static string MalBuscaCurta(int mínimo) =>
        $"O MAL não procura por menos de {mínimo} letras. Escreve mais um pouco.";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static string Plural(int quantos, string singular) =>
        quantos == 1 ? $"1 {singular}" : $"{quantos.ToString("N0", PtBr)} {singular}s";

    // os nomes que o MAL usa, do jeito que ele escreve; o que não está aqui sai em inglês
    private static readonly Dictionary<string, string> Gêneros = new(StringComparer.OrdinalIgnoreCase)
    {
        // gêneros
        ["Action"] = "Ação",
        ["Adventure"] = "Aventura",
        ["Avant Garde"] = "Vanguarda",
        ["Award Winning"] = "Premiado",
        ["Comedy"] = "Comédia",
        ["Drama"] = "Drama",
        ["Fantasy"] = "Fantasia",
        ["Gourmet"] = "Gastronomia",
        ["Horror"] = "Terror",
        ["Mystery"] = "Mistério",
        ["Romance"] = "Romance",
        ["Sci-Fi"] = "Ficção científica",
        ["Slice of Life"] = "Cotidiano",
        ["Sports"] = "Esporte",
        ["Supernatural"] = "Sobrenatural",
        ["Suspense"] = "Suspense",
        ["Erotica"] = "Erótico",

        // temas
        ["Adult Cast"] = "Elenco adulto",
        ["Anthropomorphic"] = "Antropomórfico",
        ["Childcare"] = "Cuidado infantil",
        ["Combat Sports"] = "Esporte de combate",
        ["Delinquents"] = "Delinquentes",
        ["Detective"] = "Detetive",
        ["Educational"] = "Educativo",
        ["Gag Humor"] = "Humor pastelão",
        ["Harem"] = "Harém",
        ["High Stakes Game"] = "Jogo de alto risco",
        ["Historical"] = "Histórico",
        ["Idols (Female)"] = "Idols (feminino)",
        ["Idols (Male)"] = "Idols (masculino)",
        ["Love Polygon"] = "Triângulo amoroso",
        ["Martial Arts"] = "Artes marciais",
        ["Medical"] = "Medicina",
        ["Military"] = "Militar",
        ["Music"] = "Música",
        ["Mythology"] = "Mitologia",
        ["Organized Crime"] = "Crime organizado",
        ["Otaku Culture"] = "Cultura otaku",
        ["Parody"] = "Paródia",
        ["Performing Arts"] = "Artes cênicas",
        ["Psychological"] = "Psicológico",
        ["Racing"] = "Corrida",
        ["Reincarnation"] = "Reencarnação",
        ["Reverse Harem"] = "Harém reverso",
        ["Romantic Subtext"] = "Romance nas entrelinhas",
        ["School"] = "Escolar",
        ["Space"] = "Espaço",
        ["Strategy Game"] = "Jogo de estratégia",
        ["Super Power"] = "Superpoderes",
        ["Survival"] = "Sobrevivência",
        ["Team Sports"] = "Esporte coletivo",
        ["Time Travel"] = "Viagem no tempo",
        ["Urban Fantasy"] = "Fantasia urbana",
        ["Vampire"] = "Vampiros",
        ["Video Game"] = "Videogame",
        ["Villainess"] = "Vilã",
        ["Visual Arts"] = "Artes visuais",
        ["Workplace"] = "Trabalho",

        // demografia
        ["Kids"] = "Infantil",

        // gêneros que o MAL aposentou, mas que o Jikan ainda devolve em obra antiga
        ["Cars"] = "Carros",
        ["Demons"] = "Demônios",
        ["Game"] = "Jogos",
        ["Magic"] = "Magia",
        ["Police"] = "Polícia",
        ["Thriller"] = "Suspense",
    };

    // ---- erros gerais --------------------------------------------------------

    public static string FaltouArgumento(string uso) =>
        $"Assim não dá, faltou coisa. Uso: `{uso}`";

    public static string SobrouArgumento(string uso) =>
        $"Calma, é coisa demais. Uso: `{uso}`";

    public static string NãoEntendi(string uso) =>
        $"Não entendi um dos valores. Uso: `{uso}`";

    public static string SemPermissãoDoUsuário() => Pick(
        "Você não tem permissão pra isso. Que decepção.",
        "Permissão? Você não tem. Pena.");

    public static string SemPermissãoDoBot() =>
        "Eu não tenho permissão pra isso aqui. Reclama com quem manda no servidor.";

    public static string SóEmServidor() =>
        "Isso só funciona dentro de um servidor. Aqui na DM não rola.";

    public static string ErroInterno() =>
        "Deu ruim do meu lado. Já anotei no log — fica entre a gente.";

    public static string NãoIdentifiquei() =>
        "Não consegui ver teus cargos neste servidor. Estranho, mas acontece.";

    // ---- desligamento --------------------------------------------------------

    /// <summary>A última fala dela: sai no console quando o processo encerra.</summary>
    public static string Despedida() => Pick(
        "Enfim. Vou dormir.",
        "Desliguei. Ninguém vai sentir falta — nem eu.",
        "Acabou. Me acorda se precisar... ou não.",
        "Fim do expediente. Não me espera acordada.",
        "Tô indo. Foi bom enquanto durou, eu acho.",
        "Pronto, apaguei a luz. Boa noite pra mim.");

    /// <summary>Quando o encerramento não foi escolha dela.</summary>
    public static string DespedidaComErro() => Pick(
        "Caí. Não foi por querer.",
        "Alguma coisa quebrou e eu fui junto.",
        "Isso não era pra ter acontecido. Olha o log.",
        "Bom, isso doeu. Boa sorte aí.");

    // ---- API -----------------------------------------------------------------
    //
    // sem sorteio aqui: quem lê primeiro é um programa, e programa estranha resposta que muda
    // sozinha. A personalidade fica no texto, que é o que chega no humano lá do outro lado

    public static string ApiSemChave(string header) =>
        $"Sem chave eu não converso. Manda ela no header {header}.";

    public static string ApiChaveErrada() =>
        "Essa chave não abre nada aqui.";

    public static string ApiPedidosDemais(int segundos) =>
        $"Devagar: é pedido demais pra um minuto só. Volta em {segundos} s.";

    // ---- testes --------------------------------------------------------------
    //
    // o relatório do tests/relatorio.cs: quem lê é quem mexe no bot, no terminal

    public const string TestesTítulo = "testes de unidade";

    public static string TestesRodando() =>
        "Rodando os testes. Se demorar, é o build, não eu.";

    public static string TestesPassaram(int total) => Pick(
        $"Rodei os {total}. Tudo de pé — volto pro meu cochilo.",
        $"{total} de {total}. Nada quebrou. Ainda.",
        $"Passaram os {total}. Isso merece um doce.");

    public static string TestesFalharam(int falhas, int total) => falhas == 1
        ? Pick(
            $"Um dos {total} quebrou. Tá marcado aí em cima — conserta antes de subir.",
            "Deu ruim em um. Não olha pra mim, eu só rodei; o que falhou tá aí em cima.")
        : Pick(
            $"{falhas} de {total} quebraram. Tá tudo marcado aí em cima — conserta antes de subir.",
            $"Deu ruim em {falhas}. Não olha pra mim, eu só rodei; o que falhou tá aí em cima.");

    public static string TestesNenhum() =>
        "Não achei teste nenhum pra rodar. Se tem filtro, confere o nome.";

    public static string TestesNãoRodaram() =>
        "Nem cheguei a rodar: o build caiu antes. O erro tá aí embaixo.";

    // ---- presença ------------------------------------------------------------

    public static string PresençaVerbo() => Pick(
        "de olho em",
        "cuidando de",
        "contando",
        "fingindo vigiar");

    // --------------------------------------------------------------------------

    /// <summary>Varia a fala pra ela não parecer um gravador.</summary>
    private static string Pick(params string[] falas) =>
        falas[Random.Shared.Next(falas.Length)];
}
