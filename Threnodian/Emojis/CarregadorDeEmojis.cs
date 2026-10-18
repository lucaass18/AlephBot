using System.Net.Http.Headers;
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

            using var lista = await LerAsync(http, $"applications/{appId}/emojis", cancellationToken);

            if (lista is null || !lista.RootElement.TryGetProperty("items", out var itens))
            {
                logger.LogWarning("O app não devolveu emoji nenhum; os rótulos ficam sem emoji");
                return;
            }

            foreach (var item in itens.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var nome) && item.TryGetProperty("id", out var emojiId)
                    && nome.GetString() is { Length: > 0 } texto && emojiId.GetString() is { Length: > 0 } valor)
                {
                    var animado = item.TryGetProperty("animated", out var a) && a.GetBoolean();

                    EmojisDaFicha.Guarda(texto, valor, animado);
                }
            }

            logger.LogInformation("Emoji do app carregados: {Quantos}", EmojisDaFicha.Quantos);
        }
        catch (Exception erro)
        {
            // emoji é enfeite: sem eles o rótulo é a palavra, e o bot sobe do mesmo jeito
            logger.LogWarning(erro, "Não consegui ler os emoji do app; os rótulos ficam sem emoji");
        }
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
