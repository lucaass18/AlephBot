using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using AlephBot.Config;

namespace AlephBot.Threnodian.Youtube;

// A porta do plugin do YouTube no Lavalink: ler e trocar o token sem yml e sem restart.
public sealed class LavalinkYoutube : IDisposable
{
    // entregar o token faz o Lavalink renovar o acesso no Google na hora, síncrono
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(20);

    private readonly HttpClient _http;

    public LavalinkYoutube(AlephConfig config)
    {
        _http = new HttpClient { BaseAddress = config.LavalinkUri, Timeout = Espera };

        // a senha vai crua no Authorization, sem esquema — é assim que o Lavalink espera
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", config.LavalinkPassword);
    }

    // O token que o plugin tem agora. Null quando não tem — ou quando o Lavalink não respondeu.
    public async Task<string?> TokenAtualAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var resposta = await _http.GetAsync("youtube", cancellationToken);

            if (!resposta.IsSuccessStatusCode)
                return null;

            var estado = await resposta.Content.ReadFromJsonAsync<Estado>(cancellationToken);

            return string.IsNullOrWhiteSpace(estado?.RefreshToken) ? null : estado.RefreshToken;
        }
        catch (Exception erro) when (Rede(erro, cancellationToken))
        {
            return null;
        }
    }

    // Entrega o token. O plugin testa no Google, então a recusa aqui é do Google.
    public async Task<Entrega> EntregarAsync(string token, CancellationToken cancellationToken = default)
    {
        // poToken e visitorData vazios: o plugin lê vazio como "não mexe" e nulo como "apaga"
        var corpo = new
        {
            refreshToken = token,
            skipInitialization = true,
            poToken = "",
            visitorData = "",
        };

        try
        {
            using var resposta = await _http.PostAsJsonAsync("youtube", corpo, cancellationToken);

            if (resposta.IsSuccessStatusCode)
                return Entrega.Aceito;

            var motivo = await MotivoAsync(resposta, cancellationToken);

            return Entrega.Recusado($"{(int)resposta.StatusCode}: {motivo}");
        }
        catch (Exception erro) when (Rede(erro, cancellationToken))
        {
            return Entrega.Indisponível;
        }
    }

    private static async Task<string> MotivoAsync(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        try
        {
            var erro = await resposta.Content.ReadFromJsonAsync<Erro>(cancellationToken);

            if (erro?.Message is { Length: > 0 } mensagem)
                return mensagem;
        }
        catch (JsonException)
        {
            // corpo que não é o JSON de erro do Lavalink; sobra o status
        }

        return resposta.ReasonPhrase ?? "sem motivo";
    }

    // Lavalink fora, lento ou falando outra língua. Cancelamento meu não entra aqui.
    private static bool Rede(Exception erro, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested
        && erro is HttpRequestException or TaskCanceledException or JsonException;

    public void Dispose() => _http.Dispose();

    // O que o Lavalink respondeu à entrega.
    public readonly record struct Entrega(Entrega.Resultado Estado, string? Motivo)
    {
        public enum Resultado
        {
            Aceito,
            Recusado,
            Indisponível,
        }

        public static Entrega Aceito => new(Resultado.Aceito, null);

        public static Entrega Indisponível => new(Resultado.Indisponível, null);

        public static Entrega Recusado(string motivo) => new(Resultado.Recusado, motivo);

        // Recusa do Google, não tropeço de rede: invalid_grant só se resolve com login novo.
        public bool TokenMorreu =>
            Motivo is not null
            && (Motivo.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase)
                || Motivo.Contains("returned error", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record Estado([property: JsonPropertyName("refreshToken")] string? RefreshToken);

    private sealed record Erro([property: JsonPropertyName("message")] string? Message);
}
