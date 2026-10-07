using Microsoft.AspNetCore.Builder;
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
        services.AddOpenApi(options => options.AddDocumentTransformer((documento, _, _) =>
        {
            documento.Info = new OpenApiInfo
            {
                Title = Título,
                Version = AlephBot.Version,
                Description =
                    "Só leitura: status e estatísticas do bot. Toda rota, menos o `/api/health`, " +
                    $"pede a `API_KEY` do `Config/.env` no header `{ApiKeyFilter.Header}`.",
            };

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
        }));

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
        app.MapOpenApi(Documento);

        app.MapScalarApiReference(Página, scalar =>
        {
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
