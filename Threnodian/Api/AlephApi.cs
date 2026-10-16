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

// A API HTTP: um Kestrel dentro do processo do bot. Só leitura, de propósito.
public static class AlephApi
{
    // A versão que está no ar. Mudança que quebra quem já usa vira v2 ao lado dela.
    public const int VersãoAtual = 1;

    // No contêiner escuto em tudo, senão a porta do compose não chega; fora dele, só localhost.
    private static readonly bool NoContêiner =
        string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);

    // Onde a API atende, do jeito que sai no log do boot.
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

        // sem apagar o ASPNETCORE_HTTP_PORTS da imagem, o Kestrel avisa a cada boot que a ignora
        builder.WebHost.UseSetting(WebHostDefaults.HttpPortsKey, string.Empty);

        builder.Services.ConfigureHttpJsonOptions(json =>
        {
            json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

            // aqui só se escreve: número sai como número, e os docs não dizem "integer ou string"
            json.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });

        // o Tailscale na frente conta HTTPS e IP real nos X-Forwarded-*; só vale vindo de dentro
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

        ConfiguraOCors(builder.Services, api);

        // assina o biscoito dos docs; a chave fica no processo, e contêiner novo pede entrada
        builder.Services.AddDataProtection();

        builder.Services.AddSingleton(api);
        builder.Services.AddSingleton<ApiKeyFilter>();
        builder.Services.AddSingleton<TentativasDeSenha>();
        builder.Services.AddSingleton<BotStatus>();

        builder.Services.AddSingleton<LavalinkMonitor>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<LavalinkMonitor>());

        // a versão vai no caminho (/api/v1/...) e o header api-supported-versions diz quais existem
        var versões = builder.Services
            .AddApiVersioning(opções =>
            {
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

    // O nome da política de CORS; o navegador é o único que se importa com ela.
    public const string Cors = "api";

    // Sem isto, a chave em header próprio obriga um OPTIONS antes, e sem resposta ele nem tenta.
    private static void ConfiguraOCors(IServiceCollection services, ApiConfig api) =>
        services.AddCors(cors => cors.AddPolicy(Cors, política =>
        {
            política
                .WithMethods(HttpMethods.Get, HttpMethods.Head)
                .WithHeaders(ApiKeyFilter.Header)

                // sem dizer quais pode ler, o navegador esconde os headers que o documento promete
                .WithExposedHeaders(
                    LimiteDePedidos.CabeçalhoDaCota,
                    LimiteDePedidos.CabeçalhoDaSobra,
                    HeaderDeEspera)

                // a permissão vale um tempo no navegador: sem isso cada chamada vira duas
                .SetPreflightMaxAge(TimeSpan.FromHours(2));

            if (api.CorsOrigins.Contains(OrigensDeConfiança.Qualquer))
                política.AllowAnyOrigin();
            else
                política.SetIsOriginAllowed(origem => OrigensDeConfiança.Confia(origem, api.CorsOrigins));
        }));

    // Quantos segundos esperar depois de um 429.
    internal const string HeaderDeEspera = "Retry-After";

    public static void Map(WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        // a ordem conta: o limite vem antes do crachá dos docs, pra chutar senha também gastar cota
        ProteçãoDeFora.UseCabeçalhos(app);

        // antes do limite: o OPTIONS não leva dado, e cobrar por ele é cobrar duas vezes
        app.UseCors(Cors);

        LimiteDePedidos.Use(app);
        ProteçãoDeFora.UseCracháDosDocs(app, app.Services.GetRequiredService<ApiConfig>());

        // o 429 vale pra toda rota, e o documento conta isso em cada uma
        var v1 = app.NewVersionedApi("AlephBot")
            .MapGroup("/api/v{version:apiVersion}")
            .HasApiVersion(VersãoAtual)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // abertas de propósito: estar de pé e quem eu sou é o que um monitor já mostraria
        var abertas = v1.MapGroup("")
            .WithTags(ApiDocs.GrupoStatus)
            .SemChave();

        abertas.MapGet("/health", Saúde)
            .WithSummary("Se o bot está de pé")
            .WithDescription(
                "`ok`, `degraded` (responde comando, mas sem música) ou `down` (sem Discord, com 503). " +
                "Não pede chave: é a rota que monitor de uptime chama.")
            .Produces<HealthResponse>()
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable)
            .ComExemplo(ExemplosDaApi.Saúde);

        // HEAD é o padrão de monitor tipo UptimeRobot; sem ele, 405 e alarme falso
        abertas.MapMethods("/health", [HttpMethods.Head], Saúde)
            .WithSummary("Se o bot está de pé, sem corpo")
            .WithDescription(
                "O mesmo do GET, só com o código: 200 de pé, 503 sem Discord. É o que monitor de uptime " +
                "costuma chamar.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        abertas.MapGet("/status", (BotStatus bot) => bot.Status())
            .WithSummary("Quem o bot é e como estão as conexões")
            .WithDescription("Versão, uptime, latência do gateway e o estado do Lavalink. Não pede chave.")
            .ComExemplo(ExemplosDaApi.Status);

        var comChave = v1.MapGroup("")
            .WithTags(ApiDocs.GrupoBot)
            .AddEndpointFilter(app.Services.GetRequiredService<ApiKeyFilter>())
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        comChave.MapGet("/stats", (BotStatus bot) => bot.Estatísticas())
            .WithSummary("Os números do bot")
            .WithDescription("Servidores, membros, pessoas online, players de música e memória.")
            .ComExemplo(ExemplosDaApi.Estatísticas);

        comChave.MapGet("/commands", (BotStatus bot) => bot.Comandos())
            .WithSummary("Os comandos do /help")
            .WithDescription("Um item por comando, com a forma em barra, a de prefixo e os atalhos.")
            .ComExemplo(ExemplosDaApi.Comandos);

        MapEndereçosDeAntes(app);
        ApiDocs.Map(app);
    }

    // 503 quando o Discord caiu: aí eu não respondo comando nenhum.
    private static JsonHttpResult<HealthResponse> Saúde(BotStatus bot)
    {
        var saúde = bot.Saúde();

        return TypedResults.Json(
            saúde,
            statusCode: saúde.Status == HealthStatus.Down
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK);
    }

    // Os endereços de antes da versão: /api/health fica, o resto vai pra atual com 308.
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
