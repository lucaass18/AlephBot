using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Anime;
using AlephBot.Core.Commands.Interface;
using AlephBot.Threnodian.MyAnimeList;
using AlephBot.Threnodian.Translation;

using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Mangá;

// /mg — a ficha do mangá. A busca é a mesma do /ma; o que muda é o MangaDex no caminho.
public sealed class MangadexCommand : AlephSlashModule
{
    private readonly IMyAnimeList _fonte;
    private readonly GoogleTranslator _tradutor;
    private readonly IFichasGuardadas _guarda;
    private readonly ISinopseEmPortuguês _mangadex;

    public MangadexCommand(
        IMyAnimeList fonte, GoogleTranslator tradutor, IFichasGuardadas guarda, ISinopseEmPortuguês mangadex)
    {
        _fonte = fonte;
        _tradutor = tradutor;
        _guarda = guarda;
        _mangadex = mangadex;
    }

    public override string Name => "mg";
    public override string Description => "Procura um mangá, com sinopse em português quando tem.";
    public override CommandCategory Category => CommandCategory.Anime;
    public override string? Usage => MyAnimelist.UsoDeMangá;

    [SlashCommand("mg", "Procura um mangá, com sinopse em português quando tem.")]
    public async Task MangáAsync(
        [SlashCommandParameter(Name = "nome", Description = "O nome do mangá.")] string nome)
    {
        // perguntar à fonte, ao MangaDex e ao tradutor passa dos 3 segundos do Discord; peço tempo
        await DeferAsync();

        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            _fonte, _tradutor, _guarda, _mangadex, TipoDeObra.Manga, nome, Context.User.Username);

        if (erro is not null)
            await EditarComErroAsync(erro);
        else
            await EditarRespostaAsync(embed!);
    }
}

// !mg, !manga — a mesma ficha do /mg, para quem prefere o prefixo.
public sealed class MangadexTextCommand : AlephTextModule
{
    private readonly IMyAnimeList _fonte;
    private readonly GoogleTranslator _tradutor;
    private readonly IFichasGuardadas _guarda;
    private readonly ISinopseEmPortuguês _mangadex;

    public MangadexTextCommand(
        IMyAnimeList fonte, GoogleTranslator tradutor, IFichasGuardadas guarda, ISinopseEmPortuguês mangadex)
    {
        _fonte = fonte;
        _tradutor = tradutor;
        _guarda = guarda;
        _mangadex = mangadex;
    }

    public override string Name => "mg";
    public override string Description => "Procura um mangá, com sinopse em português quando tem.";
    public override CommandCategory Category => CommandCategory.Anime;
    public override string? Usage => MyAnimelist.UsoDeMangá;

    // escondido do /help pra não duplicar a entrada do slash
    public override bool IsHidden => true;

    [Command("mg", "manga", "mangá")]
    public async Task MangáAsync([CommandParameter(Remainder = true)] string nome)
    {
        // a resposta demora uns segundos; o "digitando..." é o jeito de dizer que eu ouvi
        await Context.Client.Rest.TriggerTypingAsync(Context.Message.ChannelId);

        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            _fonte, _tradutor, _guarda, _mangadex, TipoDeObra.Manga, nome, Context.User.Username);

        if (erro is not null)
            await ErrorAsync(erro);
        else
            await ReplyAsync(embed!);
    }
}
