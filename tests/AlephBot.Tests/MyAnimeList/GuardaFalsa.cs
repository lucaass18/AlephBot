using AlephBot.Threnodian.MyAnimeList;

namespace AlephBot.Tests.MyAnimeList;

/// <summary>
/// Uma guarda de mentira: diz o que foi guardado e devolve o que mandarem devolver. Com
/// <see cref="Disponível"/> falso ela é o bot rodando sem Postgres.
/// </summary>
internal sealed class GuardaFalsa : IFichasGuardadas
{
    public bool Disponível { get; init; } = true;

    /// <summary>A ficha dentro da validade, que dispensa perguntar à fonte.</summary>
    public Obra? Recente { get; init; }

    /// <summary>A ficha vencida, que só serve quando a fonte caiu.</summary>
    public Obra? Velha { get; init; }

    public Obra? Guardada { get; private set; }

    public string? BuscaGuardada { get; private set; }

    public Task<Obra?> AcharAsync(
        TipoDeObra tipo, string busca, TimeSpan? validade, CancellationToken cancellationToken = default) =>
        Task.FromResult(validade is null ? Velha : Recente);

    public Task GuardarAsync(string busca, Obra obra, CancellationToken cancellationToken = default)
    {
        BuscaGuardada = busca;
        Guardada = obra;

        return Task.CompletedTask;
    }
}

/// <summary>
/// O MangaDex que nunca casa a obra. Serve aos testes que não são sobre a sinopse em
/// português: com ele, a ficha segue o caminho de sempre.
/// </summary>
internal sealed class SemMangaDex : ISinopseEmPortuguês
{
    public Task<string?> ParaAsync(Obra obra, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
