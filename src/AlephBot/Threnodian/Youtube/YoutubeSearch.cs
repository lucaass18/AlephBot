using System.Net.Http.Json;
using System.Text.Json;

using AlephBot.Threnodian.Rede;

namespace AlephBot.Threnodian.Youtube;

// Busca de playlist pelo nome: o plugin só procura vídeo, então pergunto à API da página.
public sealed class YoutubeSearch : IDisposable
{
    private const string UrlBusca = "https://www.youtube.com/youtubei/v1/search?prettyPrint=false";

    // O "Tipo: Playlist" do filtro da página — é o que vai no sp= quando você marca ele.
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

    private readonly HttpClient _http = ClienteHttp.Novo(TimeSpan.FromSeconds(15));

    public YoutubeSearch()
    {
        // sem este cookie, de IP europeu a resposta é a tela de consentimento, não a busca
        _http.DefaultRequestHeaders.Add("Cookie", "SOCS=CAI");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    }

    // O link que o Lavalink entende pra uma playlist achada aqui.
    public static string LinkDaPlaylist(string id) => $"https://www.youtube.com/playlist?list={id}";

    // O ID da primeira playlist, ou null. Erro de rede sobe como exceção, pro chamador falar.
    public async Task<string?> PlaylistAsync(string busca, CancellationToken cancellationToken = default)
    {
        var corpo = new { context = Contexto, query = busca, @params = FiltroPlaylist };

        using var resposta = await _http.PostAsJsonAsync(UrlBusca, corpo, cancellationToken);

        resposta.EnsureSuccessStatusCode();

        await using var conteúdo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(conteúdo, cancellationToken: cancellationToken);

        return PrimeiraPlaylist(json.RootElement);
    }

    // Procura o primeiro cartão de playlist: a ordem do JSON é a ordem da página.
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
