using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Translation;

/// <summary>
/// Tradução pelo Google, sem chave: o mesmo endpoint que a extensão de dicionário do Chrome
/// usa. Serve pra sinopse do /ma, que o MAL só tem em inglês. Não é API pública, então
/// quando ela falha eu não insisto — devolvo null e quem chamou mostra o original.
/// </summary>
public sealed class GoogleTranslator : IDisposable
{
    private const string Endereço = "https://clients5.google.com/translate_a/t?client=dict-chrome-ex";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly ILogger<GoogleTranslator> _logger;

    public GoogleTranslator(ILogger<GoogleTranslator> logger)
    {
        _logger = logger;

        // o endpoint é da extensão do Chrome; me apresento como o navegador que ela roda
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    }

    /// <summary>
    /// O texto em <paramref name="para"/>, ou null quando o Google não colaborou. Os
    /// parágrafos sobrevivem à tradução; a formatação é do chamador.
    /// </summary>
    public async Task<string?> TraduzirAsync(
        string texto, string de = "en", string para = "pt", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return texto;

        try
        {
            // o texto vai no corpo: sinopse grande não cabe numa URL
            using var corpo = new FormUrlEncodedContent([new KeyValuePair<string, string>("q", texto)]);
            using var resposta = await _http.PostAsync($"{Endereço}&sl={de}&tl={para}", corpo, cancellationToken);

            if (!resposta.IsSuccessStatusCode)
            {
                // 429 é o Google achando que sou robô. Ele não está errado
                _logger.LogWarning("O tradutor respondeu {Status}; a sinopse fica em inglês", (int)resposta.StatusCode);
                return null;
            }

            await using var conteúdo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(conteúdo, cancellationToken: cancellationToken);

            return Traduzido(json.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "O tradutor não respondeu; a sinopse fica em inglês");
            return null;
        }
    }

    /// <summary>
    /// A resposta é <c>["texto"]</c> com a língua de origem fixa, e <c>[["texto","en"]]</c>
    /// quando ela é detectada. Aceito as duas: o formato não é documentado e já mudou antes.
    /// </summary>
    private static string? Traduzido(JsonElement raiz)
    {
        if (raiz.ValueKind != JsonValueKind.Array || raiz.GetArrayLength() == 0)
            return null;

        var primeiro = raiz[0];

        if (primeiro.ValueKind == JsonValueKind.Array && primeiro.GetArrayLength() > 0)
            primeiro = primeiro[0];

        var texto = primeiro.ValueKind == JsonValueKind.String ? primeiro.GetString() : null;

        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    public void Dispose() => _http.Dispose();
}
