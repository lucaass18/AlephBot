using AlephBot.Core.Commands.Interface;

namespace AlephBot.Core.Personality;

/// <summary>
/// Personalidade do bot: Denia (Wuthering Waves) — niilista de bom humor, sonolenta,
/// gulosa e cúmplice. Alfineta, mas nunca esconde a informação atrás da piada:
/// toda mensagem daqui continua dizendo o que aconteceu e o que fazer.
///
/// Todo texto que o usuário lê sai deste arquivo. Mudar o tom do bot é mexer só aqui.
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
