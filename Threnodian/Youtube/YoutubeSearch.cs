using System.Net.Http.Json;
using System.Text.Json;

namespace AlephBot.Threnodian.Youtube;

/// <summary>
/// Busca de playlist do YouTube pelo nome.
///
/// O plugin do Lavalink só procura vídeo: <c>ytsearch:</c> devolve faixas, e playlist só
/// entra por link. Pra "nightcore rock" virar uma playlist eu pergunto ao YouTube pela
/// mesma API interna que a página de resultados usa, com o filtro "só playlists", e fico
/// com o ID da primeira. Quem carrega as faixas depois é o Lavalink, pelo link normal.
/// </summary>
public sealed class YoutubeSearch : IDisposable
{
    private const string UrlBusca = "https://www.youtube.com/youtubei/v1/search?prettyPrint=false";

    /// <summary>
    /// O "Tipo: Playlist" do filtro da página de resultados — é o que vai no <c>sp=</c> da
    /// URL quando você marca ele no site.
    /// </summary>
    private const string FiltroPlaylist = "EgIQAw%3D%3D";

    private const string TipoPlaylist = "LOCKUP_CONTENT_TYPE_PLAYLIST";

    // o mesmo client que o navegador manda; a versão não precisa ser a do dia, só ser recente
    private static readonly object Contexto = new
    {
        client = new
        {
            clientName = "WEB",
            clientVersion = "2.20250101.00.00",
            hl = "pt",
            gl = "BR",
        },
    };

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public YoutubeSearch()
    {
        // sem este cookie, de IP europeu a resposta é a tela de consentimento, não a busca
        _http.DefaultRequestHeaders.Add("Cookie", "SOCS=CAI");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    }

    /// <summary>O link que o Lavalink entende pra uma playlist achada aqui.</summary>
    public static string LinkDaPlaylist(string id) => $"https://www.youtube.com/playlist?list={id}";

    /// <summary>
    /// O ID da primeira playlist que o YouTube devolve pra <paramref name="busca"/>, ou null
    /// quando não vem nenhuma. Problema de rede sobe como exceção: é o chamador quem sabe
    /// dizer isso do jeito certo.
    /// </summary>
    public async Task<string?> PlaylistAsync(string busca, CancellationToken cancellationToken = default)
    {
        var corpo = new { context = Contexto, query = busca, @params = FiltroPlaylist };

        using var resposta = await _http.PostAsJsonAsync(UrlBusca, corpo, cancellationToken);

        resposta.EnsureSuccessStatusCode();

        await using var conteúdo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(conteúdo, cancellationToken: cancellationToken);

        return PrimeiraPlaylist(json.RootElement);
    }

    /// <summary>
    /// Anda pela resposta inteira atrás do primeiro cartão de playlist. A ordem do JSON é a
    /// ordem da página, então o primeiro que aparece é o primeiro resultado.
    /// </summary>
    private static string? PrimeiraPlaylist(JsonElement elemento)
    {
        switch (elemento.ValueKind)
        {
            case JsonValueKind.Object:
                // o cartão de hoje: um lockup genérico, que diz o tipo do que carrega
                if (elemento.TryGetProperty("lockupViewModel", out var lockup)
                    && lockup.TryGetProperty("contentType", out var tipo)
                    && tipo.ValueEquals(TipoPlaylist)
                    && lockup.TryGetProperty("contentId", out var id))
                {
                    return id.GetString();
                }

                // o cartão antigo, que o YouTube ainda serve pra alguns clientes
                if (elemento.TryGetProperty("playlistRenderer", out var cartão)
                    && cartão.TryGetProperty("playlistId", out var idAntigo))
                {
                    return idAntigo.GetString();
                }

                foreach (var propriedade in elemento.EnumerateObject())
                {
                    if (PrimeiraPlaylist(propriedade.Value) is { } achou)
                        return achou;
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in elemento.EnumerateArray())
                {
                    if (PrimeiraPlaylist(item) is { } achou)
                        return achou;
                }

                break;
        }

        return null;
    }

    public void Dispose() => _http.Dispose();
}
