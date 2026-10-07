using System.Text.Json;
using System.Text.Json.Serialization;

using AlephBot.Config;

using Asp.Versioning;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
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
    /// <summary>A versão que está no ar. Mudança que quebra quem já usa vira v2 ao lado dela.</summary>
    public const int VersãoAtual = 1;

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

            // o padrão web aceita número vindo como texto, e a página dos docs anunciava "integer ou
            // string" em todo campo. Aqui só se escreve — e número sai sempre como número
            json.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });

        // na frente de mim fica o Tailscale (serve e Funnel), que conta em X-Forwarded-Proto se o
        // pedido chegou por HTTPS e em X-Forwarded-For quem pediu. Sem ler isso a página pública
        // anunciava "http://" — e o navegador barrava o botão de testar — e o log de chave errada
        // mostrava o IP do Docker. Só vale vindo da rede de dentro: de fora, qualquer um forjaria
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var rede in RedeDeDentro.Redes)
                options.KnownIPNetworks.Add(rede);
        });

        // erro sai como application/problem+json, igual pra 401, 404 e exceção
        builder.Services.AddProblemDetails();

        builder.Services.AddSingleton(api);
        builder.Services.AddSingleton<ApiKeyFilter>();
        builder.Services.AddSingleton<BotStatus>();

        builder.Services.AddSingleton<LavalinkMonitor>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<LavalinkMonitor>());

        // a versão vai no caminho (/api/v1/...), e toda resposta versionada conta no header
        // api-supported-versions quais versões existem: o v2 nasce do lado do v1, sem quebrar ninguém
        var versões = builder.Services
            .AddApiVersioning(opções =>
            {
                opções.DefaultApiVersion = new ApiVersion(VersãoAtual);
                opções.ApiVersionReader = new UrlSegmentApiVersionReader();
                opções.ReportApiVersions = true;
            })
            .AddApiExplorer(opções =>
            {
                // "v1" é o nome do grupo e do documento; /api/v{version} vira /api/v1 no documento
                opções.GroupNameFormat = "'v'V";
                opções.SubstituteApiVersionInUrl = true;
            });

        ApiDocs.Configure(versões);
        LimiteDePedidos.Configure(builder.Services);
    }

    public static void Map(WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        // a ordem conta: os headers de proteção valem até pro 429, e o limite vem antes da
        // senha dos docs, pra que chutar senha também gaste a cota
        ProteçãoDeFora.UseCabeçalhos(app);
        LimiteDePedidos.Use(app);
        ProteçãoDeFora.UseSenhaDosDocs(app, app.Services.GetRequiredService<ApiConfig>());

        // o 429 vale pra toda rota, e o documento conta isso em cada uma
        var v1 = app.NewVersionedApi("AlephBot")
            .MapGroup("/api/v{version:apiVersion}")
            .HasApiVersion(VersãoAtual)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // aberta de propósito: é o que monitor de uptime chama, e não conta nada além de
        // "estou de pé". HEAD junto porque é o padrão de monitor como o UptimeRobot; sem ele,
        // 405 e alarme falso
        v1.MapMethods("/health", [HttpMethods.Get, HttpMethods.Head], Saúde)
            .WithSummary("Se o bot está de pé")
            .WithDescription(
                "`ok`, `degraded` (responde comando, mas sem música) ou `down` (sem Discord, com 503). " +
                "Não pede chave: é a rota que monitor de uptime chama.")
            .Produces<HealthResponse>()
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable)
            .SemChave();

        // também aberta: quem o bot é e se as conexões dele estão de pé é o que uma página de
        // status mostraria pra qualquer um. Os números e a lista de comandos seguem com chave
        v1.MapGet("/status", (BotStatus bot) => bot.Status())
            .WithSummary("Quem o bot é e como estão as conexões")
            .WithDescription("Versão, uptime, latência do gateway e o estado do Lavalink. Não pede chave.")
            .SemChave();

        var comChave = v1.MapGroup("")
            .AddEndpointFilter(app.Services.GetRequiredService<ApiKeyFilter>())
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        comChave.MapGet("/stats", (BotStatus bot) => bot.Estatísticas())
            .WithSummary("Os números do bot")
            .WithDescription("Servidores, membros, pessoas online, players de música e memória.");

        comChave.MapGet("/commands", (BotStatus bot) => bot.Comandos())
            .WithSummary("Os comandos do /help")
            .WithDescription("Um item por comando, com a forma em barra, a de prefixo e os atalhos.");

        MapEndereçosDeAntes(app);
        ApiDocs.Map(app);
    }

    /// <summary>503 quando o Discord caiu: aí eu não respondo comando nenhum.</summary>
    private static JsonHttpResult<HealthResponse> Saúde(BotStatus bot)
    {
        var saúde = bot.Saúde();

        return TypedResults.Json(
            saúde,
            statusCode: saúde.Status == HealthStatus.Down
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK);
    }

    /// <summary>
    /// Os endereços de antes da versão. O /api/health fica de pé como está, porque é ele que
    /// monitor de uptime chama, e monitor não lê changelog. Os outros mandam pra versão atual
    /// com 308, que preserva o método. Nenhum deles aparece nos docs.
    /// </summary>
    private static void MapEndereçosDeAntes(WebApplication app)
    {
        app.MapMethods("/api/health", [HttpMethods.Get, HttpMethods.Head], Saúde)
            .ExcludeFromDescription();

        foreach (var rota in (string[])["status", "stats", "commands"])
        {
            app.MapGet($"/api/{rota}", () => Results.Redirect($"/api/v{VersãoAtual}/{rota}", permanent: true, preserveMethod: true))
                .ExcludeFromDescription();
        }
    }
}
