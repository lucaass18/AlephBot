using AlephBot.Threnodian.MyAnimeList;

namespace AlephBot.Tests.MyAnimeList;

// Uma guarda de mentira. Com Disponível falso ela é o bot rodando sem Postgres.
internal sealed class GuardaFalsa : IFichasGuardadas
{
    public bool Disponível { get; init; } = true;

    // A ficha dentro da validade, que dispensa perguntar à fonte.
    public Obra? Recente { get; init; }

    // A ficha vencida, que só serve quando a fonte caiu.
    public Obra? Velha { get; init; }

    public Obra? Guardada { get; private set; }

    public string? BuscaGuardada { get; private set; }

    public Task<Obra?> AcharAsync(
        TipoDeObra tipo, string busca, TimeSpan? validade, CancellationToken cancellationToken = default) =>
        Task.FromResult(validade is null ? Velha : Recente);

    // As buscas que o /ma deu por não achadas. A guarda de verdade lembra; aqui eu só anoto.
    public List<string> DeramEmNada { get; } = [];

    public Task GuardarAsync(string busca, Obra obra, CancellationToken cancellationToken = default)
    {
        BuscaGuardada = busca;
        Guardada = obra;

        return Task.CompletedTask;
    }

    public bool JáDeuEmNada(TipoDeObra tipo, string busca) => false;

    public void DeuEmNada(TipoDeObra tipo, string busca) => DeramEmNada.Add(busca);
}

// O MangaDex que nunca casa a obra, pros testes que não são sobre a sinopse.
internal sealed class SemMangaDex : ISinopseEmPortuguês
{
    public Task<string?> ParaAsync(Obra obra, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
