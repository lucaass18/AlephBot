using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AlephBot.Config;
using AlephBot.Threnodian.Rede;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Emojis;

// Pergunta ao Discord quais emoji o app tem e guarda os ids. Roda uma vez, no boot: os ids
// não mudam sozinhos, e subir emoji novo é coisa de quem mexe no app, não do bot.
public sealed class CarregadorDeEmojis(AlephConfig config, ILogger<CarregadorDeEmojis> logger)
    : IHostedService
{
    private const string Api = "https://discord.com/api/v10";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var http = ClienteHttp.Novo(TimeSpan.FromSeconds(10), new Uri(Api + "/"));

            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", config.Token);

            using var aplicação = await LerAsync(http, "applications/@me", cancellationToken);

            if (aplicação is null
                || !aplicação.RootElement.TryGetProperty("id", out var id)
                || id.GetString() is not { Length: > 0 } appId)
            {
                logger.LogWarning("Não consegui descobrir o id do app; os rótulos ficam sem emoji");
                return;
            }

            var noApp = await MapeiaAsync(http, appId, cancellationToken);

            // o que o dll traz e o app ainda não tem: no primeiro boot é tudo, depois é nada
            var faltam = EmojisEmbutidos.QuaisFaltam(noApp.Keys);

            if (faltam.Count > 0)
            {
                logger.LogInformation("Subindo {Quantos} emoji que o app ainda não tem", faltam.Count);

                foreach (var nome in faltam)
                    await SobeAsync(http, appId, nome, cancellationToken);

                noApp = await MapeiaAsync(http, appId, cancellationToken);
            }

            foreach (var (nome, marcação) in noApp)
                EmojisDaFicha.Guarda(nome, marcação.Id, marcação.Animado);

            logger.LogInformation("Emoji do app carregados: {Quantos}", EmojisDaFicha.Quantos);
        }
        catch (Exception erro)
        {
            // emoji é enfeite: sem eles o rótulo é a palavra, e o bot sobe do mesmo jeito
            logger.LogWarning(erro, "Não consegui ler os emoji do app; os rótulos ficam sem emoji");
        }
    }

    // O que o app tem agora, por nome.
    private static async Task<Dictionary<string, (string Id, bool Animado)>> MapeiaAsync(
        HttpClient http, string appId, CancellationToken cancellationToken)
    {
        var mapa = new Dictionary<string, (string, bool)>(StringComparer.Ordinal);

        using var lista = await LerAsync(http, $"applications/{appId}/emojis", cancellationToken);

        if (lista is null || !lista.RootElement.TryGetProperty("items", out var itens))
            return mapa;

        foreach (var item in itens.EnumerateArray())
        {
            if (item.TryGetProperty("name", out var nome) && item.TryGetProperty("id", out var id)
                && nome.GetString() is { Length: > 0 } texto && id.GetString() is { Length: > 0 } valor)
            {
                mapa[texto] = (valor, item.TryGetProperty("animated", out var a) && a.GetBoolean());
            }
        }

        return mapa;
    }

    private async Task SobeAsync(
        HttpClient http, string appId, string nome, CancellationToken cancellationToken)
    {
        if (EmojisEmbutidos.Bytes(nome) is not { Length: > 0 } png)
            return;

        var corpo = JsonContent.Create(new
        {
            name = nome,
            image = EmojisEmbutidos.ParaDataUri(png),
        });

        using var resposta = await http.PostAsync($"applications/{appId}/emojis", corpo, cancellationToken);

        if (resposta.IsSuccessStatusCode)
            logger.LogInformation("Emoji {Nome} subido pro app", nome);
        else
            logger.LogWarning("Não consegui subir o emoji {Nome}: {Status}", nome, (int)resposta.StatusCode);
    }

    private static async Task<JsonDocument?> LerAsync(
        HttpClient http, string caminho, CancellationToken cancellationToken)
    {
        using var resposta = await http.GetAsync(caminho, cancellationToken);

        if (!resposta.IsSuccessStatusCode)
            return null;

        await using var conteúdo = await resposta.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonDocument.ParseAsync(conteúdo, cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
