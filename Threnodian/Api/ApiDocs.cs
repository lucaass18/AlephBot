using System.Text.Json;
using System.Text.RegularExpressions;

using AlephBot.Config;

using Asp.Versioning;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

using Scalar.AspNetCore;

namespace AlephBot.Threnodian.Api;

/// <summary>
/// O /api/docs: a API no navegador, pelo Scalar — cada rota, o formato da resposta e um botão
/// pra testar. Os documentos por trás (/api/openapi/v1.json, um por versão) saem das próprias
/// rotas, então não existe arquivo de documentação pra ficar desatualizado.
///
/// Essas rotas ficam fora da chave de propósito: o navegador não manda header ao abrir uma
/// página, e elas só contam o formato da API, nada de dentro do bot. A chave você cola uma
/// vez no Scalar, e é ele quem manda o X-Api-Key nos testes.
/// </summary>
public static partial class ApiDocs
{
    public const string Página = "/api/docs";

    private const string Documento = "/api/openapi/{documentName}.json";
    private const string DocumentoDeAntes = "/api/openapi.json";
    private const string Módulo = "/api/docs/aleph.js";
    private const string Título = "AlephBot API";

    // o nome com que o documento e o Scalar se referem à chave
    private const string Esquema = "ApiKey";

    /// <summary>
    /// Um documento por versão da API, todos com o mesmo acabamento: título, servidor sem barra,
    /// a chave e o nome de cada operação. A versão do documento é a da API (1.0), não a do bot.
    /// </summary>
    public static void Configure(IApiVersioningBuilder versões) =>
        versões.AddOpenApi(versionado =>
        {
            var options = versionado.Document;

            options.AddDocumentTransformer((documento, _, _) =>
            {
                documento.Info ??= new OpenApiInfo();
                documento.Info.Title = Título;
                documento.Info.Description =
                    "Só leitura: status e estatísticas do bot. Toda rota, menos `health` e `status`, " +
                    $"pede a `API_KEY` do `Config/.env` no header `{ApiKeyFilter.Header}`.";

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
    /// "GET api/v1/status" vira "getStatus": a versão já mora no documento (um por versão). O
    /// health atende GET e HEAD na mesma rota, e por isso o método entra no nome: "getHealth"
    /// e "headHealth", sem repetir.
    /// </summary>
    private static string IdDaOperação(ApiDescription rota)
    {
        var caminho = PrefixoDaApi().Replace(rota.RelativePath ?? "", "");

        var partes = caminho
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(parte => char.ToUpperInvariant(parte[0]) + parte[1..]);

        return (rota.HttpMethod ?? "get").ToLowerInvariant() + string.Concat(partes);
    }

    // "api/" e, se tiver, a versão logo depois: "api/v1/", "api/v2/"
    [GeneratedRegex("^api/(?:v[^/]+/)?")]
    private static partial Regex PrefixoDaApi();

    /// <summary>
    /// Marca no documento a rota que não pede a chave. Quem decide de verdade é o
    /// <see cref="ApiKeyFilter"/>, que só vigia o grupo com chave; isto é o aviso pro Scalar. O
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

        // /api/openapi/v1.json, e um arquivo novo pra cada versão que nascer
        app.MapOpenApi(Documento).WithDocumentPerVersion();

        // o endereço de antes da versão manda pro documento da versão atual
        app.MapGet(DocumentoDeAntes, () => Results.Redirect(
                Documento.Replace("{documentName}", $"v{AlephApi.VersãoAtual}"), permanent: true))
            .ExcludeFromDescription();

        // a IA do Scalar não lê o formulário da página: ela só usa a chave que já estiver
        // guardada no navegador pro documento dela, e essa guarda começa vazia. Este módulo
        // carrega antes do Scalar e preenche a guarda com a API_KEY — só pra quem está dentro
        if (api.ScalarAgentKey is not null)
        {
            app.MapGet(Módulo, (HttpContext http) =>
            {
                http.Response.Headers.CacheControl = "no-store";

                return Results.Text(
                    RedeDeDentro.ÉDeDentro(http) ? MóduloDaChave(api.Key) : "export default {};",
                    "text/javascript");
            })
            .ExcludeFromDescription();
        }

        app.MapScalarApiReference(Página, (scalar, http) =>
        {
            var deDentro = RedeDeDentro.ÉDeDentro(http);

            // a IA do Scalar mora no navegador: a chave dela vai no HTML de quem abre a página.
            // Em localhost ela vem com uma cota grátis; com a chave, pra todo mundo. A API_KEY é
            // que não sai daqui: de fora a IA conversa e chama o que é aberto (health e status),
            // e o resto leva 401. Quem guarda as mensagens pagas é a senha dos docs
            if (api.ScalarAgentKey is { } chave)
            {
                scalar.WithAgentKey(chave);

                if (deDentro)
                    scalar.JavaScriptConfiguration = Módulo;
            }

            scalar.Title = Título;
            scalar.OpenApiRoutePattern = Documento;

            // uma entrada por versão; com mais de uma, o Scalar mostra a escolha num menu
            foreach (var versão in app.DescribeApiVersions())
            {
                scalar.AddDocument(
                    versão.GroupName,
                    versão.IsDeprecated ? $"{Título} {versão.GroupName} (descontinuada)" : $"{Título} {versão.GroupName}",
                    isDefault: versão.ApiVersion.MajorVersion == AlephApi.VersãoAtual);
            }

            // em casa a chave fica guardada no navegador, porque colar de novo a cada F5 cansa.
            // Em máquina dos outros ela não fica pra trás: some quando a aba fecha
            scalar.PersistentAuthentication = deDentro;
            scalar.AddPreferredSecuritySchemes([Esquema]);

            // a página é pra uso da casa: sem telemetria do Scalar e sem fonte vinda de CDN
            scalar.Telemetry = false;
            scalar.DefaultFonts = false;
        });
    }

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
