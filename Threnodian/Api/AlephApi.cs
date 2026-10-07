using System.Text.Json;
using System.Text.Json.Serialization;

using AlephBot.Config;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AlephBot.Threnodian.Api;

/// <summary>
/// A API HTTP: um Kestrel dentro do mesmo processo do bot, lendo direto do gateway, dos
/// players e do registry — nada de segundo serviço nem de canal entre processos.
///
/// Só leitura, de propósito: status e estatísticas. Mandar no bot pela API (tocar, banir)
/// seria dar a qualquer um com a chave o poder de um admin, e isso pede outra conversa.
/// </summary>
public static class AlephApi
{
    /// <summary>
    /// A imagem oficial do .NET se anuncia com esta variável. Dentro do contêiner eu escuto em
    /// todas as interfaces, senão a porta publicada pelo compose não chega em mim; fora dele,
    /// só em localhost — API de bot não tem o que fazer na rede de casa.
    /// </summary>
    private static readonly bool NoContêiner =
        string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Onde a API atende, do jeito que sai no log do boot.</summary>
    public static string Endereço(ApiConfig api) =>
        NoContêiner
            ? $"porta {api.Port} do contêiner"
            : $"http://localhost:{api.Port}/api";

    public static void Configure(WebApplicationBuilder builder, ApiConfig api)
    {
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            // ninguém precisa saber que servidor está atrás da porta
            kestrel.AddServerHeader = false;

            if (NoContêiner)
                kestrel.ListenAnyIP(api.Port);
            else
                kestrel.ListenLocalhost(api.Port);
        });

        // a imagem aspnet já vem com ASPNETCORE_HTTP_PORTS=8080. Sem apagar, o Kestrel avisa a
        // cada boot que vai ignorar essa porta em favor da de cima — aviso sobre nada
        builder.WebHost.UseSetting(WebHostDefaults.HttpPortsKey, string.Empty);

        builder.Services.ConfigureHttpJsonOptions(json =>
        {
            json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

            // o padrão web aceita número vindo como texto, e o /api/docs anunciava "integer ou
            // string" em todo campo. Aqui só se escreve — e número sai sempre como número
            json.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });

        // erro sai como application/problem+json, igual pra 401, 404 e exceção
        builder.Services.AddProblemDetails();

        builder.Services.AddSingleton(api);
        builder.Services.AddSingleton<ApiKeyFilter>();
        builder.Services.AddSingleton<BotStatus>();

        builder.Services.AddSingleton<LavalinkMonitor>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<LavalinkMonitor>());

        ApiDocs.Configure(builder.Services);
    }

    public static void Map(WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        // aberta de propósito: é o que monitor de uptime chama, e não conta nada além de
        // "estou de pé". 503 quando o Discord caiu — aí eu não respondo comando nenhum.
        // HEAD junto porque é o padrão de monitor como o UptimeRobot; sem ele, 405 e alarme falso
        app.MapMethods("/api/health", [HttpMethods.Get, HttpMethods.Head], (BotStatus bot) =>
        {
            var saúde = bot.Saúde();

            return TypedResults.Json(
                saúde,
                statusCode: saúde.Status == HealthStatus.Down
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status200OK);
        })
        .WithSummary("Se o bot está de pé")
        .WithDescription(
            "`ok`, `degraded` (responde comando, mas sem música) ou `down` (sem Discord, com 503). " +
            "Não pede chave: é a rota que monitor de uptime chama.")
        .Produces<HealthResponse>()
        .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable)
        .SemChave();

        var api = app.MapGroup("/api")
            .AddEndpointFilter(app.Services.GetRequiredService<ApiKeyFilter>())
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        api.MapGet("/status", (BotStatus bot) => bot.Status())
            .WithSummary("Quem o bot é e como estão as conexões")
            .WithDescription("Versão, uptime, latência do gateway e o estado do Lavalink.");

        api.MapGet("/stats", (BotStatus bot) => bot.Estatísticas())
            .WithSummary("Os números do bot")
            .WithDescription("Servidores, membros, pessoas online, players de música e memória.");

        api.MapGet("/commands", (BotStatus bot) => bot.Comandos())
            .WithSummary("Os comandos do /help")
            .WithDescription("Um item por comando, com a forma em barra, a de prefixo e os atalhos.");

        ApiDocs.Map(app);
    }
}
