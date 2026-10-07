using System.Net;
using System.Text.Json;

using AlephBot.Config;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
    private const string Módulo = "/api/docs/aleph.js";
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

                // a chave vale pro documento inteiro, e não rota por rota: a IA do Scalar escolhe
                // a autenticação das chamadas dela pelo primeiro item daqui — sem ele, chama sem
                // header nenhum e leva 401 mesmo com a chave preenchida. Quem não pede chave avisa
                // na própria rota (SemChave)
                documento.Security =
                [
                    new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(Esquema, documento)] = [] },
                ];

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
    /// Marca no documento a rota que não pede a chave. Quem decide de verdade é o
    /// <see cref="ApiKeyFilter"/>, que só vigia o grupo /api; isto é o aviso pro Scalar. O
    /// "[{}]" é "sem autenticação", e não "[]": lista vazia some do documento e a rota herdaria
    /// a chave do documento inteiro.
    /// </summary>
    public static TBuilder SemChave<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddOpenApiOperationTransformer((operação, _, _) =>
        {
            operação.Security = [new OpenApiSecurityRequirement()];
            return Task.CompletedTask;
        });

    public static void Map(WebApplication app)
    {
        var api = app.Services.GetRequiredService<ApiConfig>();

        app.MapOpenApi(Documento);

        // a IA do Scalar não lê o formulário da página: ela só usa a chave que já estiver
        // guardada no navegador pro documento dela, e essa guarda começa vazia. Este módulo
        // carrega antes do Scalar e preenche a guarda com a API_KEY
        if (api.ScalarAgentKey is not null)
        {
            app.MapGet(Módulo, (HttpContext http) =>
            {
                http.Response.Headers.CacheControl = "no-store";

                return Results.Text(
                    PodeLevarAChave(http) ? MóduloDaChave(api.Key) : "export default {};",
                    "text/javascript");
            })
            .ExcludeFromDescription();
        }

        app.MapScalarApiReference(Página, scalar =>
        {
            // a IA do Scalar: em localhost ela vem com uma cota grátis; fora dele, só com chave
            if (api.ScalarAgentKey is { } chave)
            {
                scalar.WithAgentKey(chave);
                scalar.JavaScriptConfiguration = Módulo;
            }

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

    /// <summary>
    /// A chave só vai pra quem já está do lado de dentro. O Funnel nem publica o /api/docs, mas
    /// se um dia publicar, o Tailscale marca o pedido e ele leva o módulo vazio; e com a porta
    /// aberta direto pra internet (API_BIND=0.0.0.0) quem chega tem IP público e também não leva.
    /// </summary>
    private static bool PodeLevarAChave(HttpContext http)
    {
        if (http.Request.Headers.ContainsKey("Tailscale-Funnel-Request"))
            return false;

        if (http.Connection.RemoteIpAddress is not { } ip)
            return false;

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        return IPAddress.IsLoopback(ip) || RedesDeDentro.Any(rede => rede.Contains(ip));
    }

    // o que chega pelo Tailscale e pelo túnel SSH entra no contêiner pela rede do Docker
    private static readonly IPNetwork[] RedesDeDentro =
    [
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("fc00::/7"),
    ];

    /// <summary>
    /// O Scalar guarda a autenticação de cada documento no localStorage, em
    /// "scalar-reference-auth-{documento}", e é lendo dali que a IA monta o header. O módulo
    /// intercepta essa leitura e põe a chave do bot — vale pro documento da página e pro do
    /// Registry, cujo nome só a IA sabe. A do bot sempre vence: uma chave velha ou errada que
    /// ficou guardada de antes não atrapalha, e trocar a API_KEY já vale no próximo F5.
    /// </summary>
    private static string MóduloDaChave(string chave) =>
        $$"""
        // AlephBot: põe a API_KEY na autenticação que o Scalar guarda no navegador
        const chave = {{JsonSerializer.Serialize(chave)}};
        const esquema = {{JsonSerializer.Serialize(Esquema)}};
        const prefixo = "scalar-reference-auth-";
        const ler = Storage.prototype.getItem;

        Storage.prototype.getItem = function (nome) {
          const valor = ler.call(this, nome);

          if (this !== window.localStorage || typeof nome !== "string" || !nome.startsWith(prefixo))
            return valor;

          let auth;
          try { auth = JSON.parse(valor ?? "{}") ?? {}; } catch { return valor; }

          auth.secrets ??= {};
          auth.secrets[esquema] = { type: "apiKey", "x-scalar-secret-token": chave };
          auth.selected ??= {};
          return JSON.stringify(auth);
        };

        export default {};
        """;
}
