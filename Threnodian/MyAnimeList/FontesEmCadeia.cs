using System.Runtime.ExceptionServices;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.MyAnimeList;

/// <summary>
/// As fontes do /ma em fila: a primeira que responder atende, e quem vier depois é reserva.
/// Existe porque o comando já dependeu de uma fonte só — o Jikan — e morreu junto com ela
/// em outubro de 2026, sem deixar no log nenhuma pista de que a culpa era de fora.
/// </summary>
public sealed class FontesEmCadeia : IMyAnimeList
{
    private readonly ILogger<FontesEmCadeia> _logger;
    private readonly IReadOnlyList<IMyAnimeList> _fontes;

    public FontesEmCadeia(ILogger<FontesEmCadeia> logger, IReadOnlyList<IMyAnimeList> fontes)
    {
        if (fontes.Count == 0)
            throw new ArgumentException("A cadeia precisa de pelo menos uma fonte.", nameof(fontes));

        _logger = logger;
        _fontes = fontes;
    }

    public string Nome => _fontes[0].Nome;

    public async Task<IReadOnlyList<Obra>> ProcurarAsync(
        TipoDeObra tipo, string busca, CancellationToken cancellationToken = default)
    {
        Exception? primeiraFalha = null;

        foreach (var fonte in _fontes)
        {
            IReadOnlyList<Obra> resultados;

            try
            {
                resultados = await fonte.ProcurarAsync(tipo, busca, cancellationToken);
            }
            catch (CredencialRecusadaException)
            {
                // credencial recusada é erro de configuração, não queda: tapar isso com a
                // reserva faria o Client ID errado funcionar calado pra sempre. 403 sozinho
                // não serve de pista — é como o AniList avisa que desligou a API
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "A fonte {Fonte} não respondeu a busca por {Busca}", fonte.Nome, busca);
                primeiraFalha ??= ex;

                continue;
            }

            if (resultados.Count > 0)
                return resultados;

            // fonte viva que não conhece a obra é tão inútil quanto fonte morta
            _logger.LogDebug("A fonte {Fonte} não achou nada para {Busca}", fonte.Nome, busca);
        }

        // ninguém respondeu: quem chamou precisa saber que foi queda, e não "não achei".
        // vai com a pilha original, que é o que diz no log onde a coisa quebrou
        if (primeiraFalha is not null)
            ExceptionDispatchInfo.Capture(primeiraFalha).Throw();

        return [];
    }
}
