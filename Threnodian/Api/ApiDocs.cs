using AlephBot.Config;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

using Scalar.AspNetCore;

namespace AlephBot.Threnodian.Api;

/// <summary>
/// O /api/docs: a API no navegador, pelo Scalar — cada rota, o formato da resposta e um botão
/// pra testar. O documento por trás (/api/openapi.json) sai das próprias rotas, então não
/// existe arquivo de documentação pra ficar desatualizado.
///
/// As duas rotas ficam fora da chave de propósito: o navegador não manda header ao abrir uma
/// página, e elas só contam o formato da API, nada de dentro do bot. A chave você cola uma
/// vez no Scalar, e é ele quem manda o X-Api-Key nos testes.
/// </summary>
public static class ApiDocs
{
    public const string Página = "/api/docs";

    private const string Documento = "/api/openapi.json";
    private const string Título = "AlephBot API";

    // o nome com que o documento e o Scalar se referem à chave
    private const string Esquema = "ApiKey";

    public static void Configure(IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((documento, _, _) =>
            {
                documento.Info = new OpenApiInfo
                {
                    Title = Título,
                    Version = AlephBot.Version,
                    Description =
                        "Só leitura: status e estatísticas do bot. Toda rota, menos o `/api/health`, " +
                        $"pede a `API_KEY` do `Config/.env` no header `{ApiKeyFilter.Header}`.",
                };

                // "http://aleph/" vira "http://aleph": servidor com barra no fim o linter do Scalar recusa
                foreach (var servidor in documento.Servers ?? [])
                    servidor.Url = servidor.Url?.TrimEnd('/');

                documento.Components ??= new OpenApiComponents();
                documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                documento.Components.SecuritySchemes[Esquema] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = ApiKeyFilter.Header,
                    Description = "A API_KEY do Config/.env.",
                };

                return Task.CompletedTask;
            });

            // o nome de cada operação: é por ele que a IA e um cliente gerado se referem à rota
            options.AddOperationTransformer((operação, contexto, _) =>
            {
                operação.OperationId ??= IdDaOperação(contexto.Description);
                return Task.CompletedTask;
            });
        });

    /// <summary>
    /// "GET api/status" vira "getStatus". O /api/health atende GET e HEAD na mesma rota, e
    /// por isso o método entra no nome: "getHealth" e "headHealth", sem repetir.
    /// </summary>
    private static string IdDaOperação(ApiDescription rota)
    {
        var caminho = rota.RelativePath ?? "";

        if (caminho.StartsWith("api/", StringComparison.Ordinal))
            caminho = caminho["api/".Length..];

        var partes = caminho
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(parte => char.ToUpperInvariant(parte[0]) + parte[1..]);

        return (rota.HttpMethod ?? "get").ToLowerInvariant() + string.Concat(partes);
    }

    /// <summary>
    /// Marca no documento as rotas que pedem a chave. Quem barra de verdade é o
    /// <see cref="ApiKeyFilter"/>; isto é só o aviso pro Scalar mandar o header nos testes.
    /// </summary>
    public static TBuilder PedeChave<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddOpenApiOperationTransformer((operação, contexto, _) =>
        {
            operação.Security =
            [
                new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(Esquema, contexto.Document)] = [] },
            ];

            return Task.CompletedTask;
        });

    public static void Map(WebApplication app)
    {
        var api = app.Services.GetRequiredService<ApiConfig>();

        app.MapOpenApi(Documento);

        app.MapScalarApiReference(Página, scalar =>
        {
            // a IA do Scalar: em localhost ela vem com uma cota grátis; fora dele, só com chave
            if (api.ScalarAgentKey is { } chave)
                scalar.WithAgentKey(chave);

            scalar.Title = Título;
            scalar.OpenApiRoutePattern = Documento;

            // a chave fica guardada neste navegador: colar de novo a cada F5 cansa
            scalar.PersistentAuthentication = true;
            scalar.AddPreferredSecuritySchemes([Esquema]);

            // a página é pra uso da casa: sem telemetria do Scalar e sem fonte vinda de CDN
            scalar.Telemetry = false;
            scalar.DefaultFonts = false;
        });
    }
}
