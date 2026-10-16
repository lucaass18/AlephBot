using System.Text.Json;
using System.Text.RegularExpressions;

using AlephBot.Config;

using Asp.Versioning;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

using Scalar.AspNetCore;

namespace AlephBot.Threnodian.Api;

// Os docs: a API no navegador pelo Scalar. O documento sai das rotas; a página fica fora da chave.
public static partial class ApiDocs
{
    // A página da versão atual; o /api/docs de antes leva pra ela.
    public static readonly string Página = $"/api/v{AlephApi.VersãoAtual}/docs";

    // /api/v1/openapi.json: um documento por versão, cada um dentro da sua
    private const string Documento = "/api/{documentName}/openapi.json";
    private static readonly string Módulo = $"{Página}/aleph.js";
    private const string Título = "AlephBot API";

    // o nome com que o documento e o Scalar se referem à chave
    private const string Esquema = "ApiKey";

    // os grupos da barra lateral: o que é aberto e o que pede chave
    public const string GrupoStatus = "Status";
    public const string GrupoBot = "Bot";

    // O topo da página, em Markdown: cada título vira um item da barra lateral.
    private static readonly string Introdução = $"""
        O estado do AlephBot, os números dele e os comandos que ele sabe. Só leitura: nada daqui
        muda o bot.

        ## Chave

        `health` e `status` são abertos. `stats` e `commands` pedem a `API_KEY` do `Config/.env`
        no header `{ApiKeyFilter.Header}` — sem ela, ou com ela errada, **401**.

        ## Limite de pedidos

        | Quem pede | Pedidos por minuto |
        |:--|:--:|
        | Pela internet | {LimiteDePedidos.DeFora} |
        | Na rede do bot | {LimiteDePedidos.DeDentro} |

        Toda resposta conta a cota em `{LimiteDePedidos.CabeçalhoDaCota}` e quanto ainda sobra em
        `{LimiteDePedidos.CabeçalhoDaSobra}`. Quem passa leva **429**, com `Retry-After` dizendo em
        quantos segundos voltar.

        ## Erros

        Erro sai como `application/problem+json`, com o motivo em `detail`:

        - **401** — sem a chave, ou com ela errada;
        - **404** — o endereço não existe (versão que não existe também, como `/api/v2/...`);
        - **429** — passou do limite de pedidos;
        - **503** — só no `health`: o bot está fora do Discord.

        ## Versões

        A versão mora no caminho: `/api/v{AlephApi.VersãoAtual}/...`. Mudança que quebra quem já usa
        nasce numa versão nova, do lado desta, que continua de pé.
        """;

    // Um documento por versão, com o mesmo acabamento. A versão é a da API (1.0), não a do bot.
    public static void Configure(IApiVersioningBuilder versões) =>
        versões.AddOpenApi(versionado =>
        {
            var options = versionado.Document;

            options.AddDocumentTransformer((documento, _, _) =>
            {
                documento.Info ??= new OpenApiInfo();
                documento.Info.Title = Título;
                documento.Info.Description = Introdução;

                // os grupos na ordem da barra lateral, cada um com a sua explicação
                documento.Tags = new HashSet<OpenApiTag>
                {
                    new()
                    {
                        Name = GrupoStatus,
                        Description = "Se o bot está de pé e quem ele é. Abertas, sem chave: é o que " +
                            "monitor de uptime e página de status chamam.",
                    },
                    new()
                    {
                        Name = GrupoBot,
                        Description = $"Os números e os comandos do bot. Pedem a `API_KEY` no header `{ApiKeyFilter.Header}`.",
                    },
                };

                // "http://aleph/" vira "http://aleph": servidor com barra no fim o Scalar recusa
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

                // a chave vale pro documento inteiro: a IA do Scalar lê o primeiro item daqui
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
                ArrumaAsRespostas(operação);
                return Task.CompletedTask;
            });
        });

    // o que cada código quer dizer aqui, no lugar do "OK" e do "Too Many Requests" do HTTP
    private static readonly Dictionary<string, string> Significados = new()
    {
        ["200"] = "Deu certo.",
        ["401"] = "Sem a chave, ou com ela errada.",
        ["429"] = "Passou do limite de pedidos: o `Retry-After` diz em quantos segundos voltar.",
        ["503"] = "O bot está fora do Discord (`down`).",
    };

    // Cada resposta com o que quer dizer e os headers do limite; sem eles, o Scalar não os vê.
    private static void ArrumaAsRespostas(OpenApiOperation operação)
    {
        if (operação.Responses is null)
            return;

        foreach (var (status, resposta) in operação.Responses)
        {
            if (resposta is not OpenApiResponse editável)
                continue;

            if (Significados.TryGetValue(status, out var significado))
                editável.Description = significado;

            editável.Headers ??= new Dictionary<string, IOpenApiHeader>();
            editável.Headers[LimiteDePedidos.CabeçalhoDaCota] = Inteiro("Quantos pedidos por minuto este IP pode fazer.");
            editável.Headers[LimiteDePedidos.CabeçalhoDaSobra] = Inteiro("Quantos ainda sobram neste minuto.");

            if (status == "429")
                editável.Headers["Retry-After"] = Inteiro("Em quantos segundos tentar de novo.");
        }
    }

    private static OpenApiHeader Inteiro(string descrição) => new()
    {
        Description = descrição,
        Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
    };

    // "GET api/v1/status" vira "getStatus"; no health o método entra no nome (GET e HEAD).
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

    // Marca a rota que não pede chave. "[{}]" é sem autenticação; lista vazia some do documento.
    public static TBuilder SemChave<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddOpenApiOperationTransformer((operação, _, _) =>
        {
            operação.Security = [new OpenApiSecurityRequirement()];
            return Task.CompletedTask;
        });

    // O exemplo da resposta 200, pelo mesmo JSON das respostas de verdade.
    public static TBuilder ComExemplo<TBuilder>(this TBuilder builder, object exemplo) where TBuilder : IEndpointConventionBuilder =>
        builder.AddOpenApiOperationTransformer((operação, contexto, _) =>
        {
            if (operação.Responses?.GetValueOrDefault("200") is { Content: { } conteúdo }
                && conteúdo.TryGetValue("application/json", out var json))
            {
                var opções = contexto.ApplicationServices
                    .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
                    .Value.SerializerOptions;

                json.Example = JsonSerializer.SerializeToNode(exemplo, exemplo.GetType(), opções);
            }

            return Task.CompletedTask;
        });

    public static void Map(WebApplication app)
    {
        var api = app.Services.GetRequiredService<ApiConfig>();
        var versão = $"v{AlephApi.VersãoAtual}";

        // /api/v1/openapi.json, e um arquivo novo pra cada versão que nascer
        app.MapOpenApi(Documento).WithDocumentPerVersion();

        LoginDosDocs.Map(app, api);

        MapEndereçosDeAntes(app, versão);

        // a raiz é a porta da frente: quem chega sem caminho nenhum cai na documentação
        app.MapGet("/", () => Results.Redirect($"{Página}/"))
            .ExcludeFromDescription();

        // a IA do Scalar só usa a chave guardada no navegador, e este módulo preenche ela
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
            var dono = ProteçãoDeFora.ÉDono(http);

            // a chave da IA vai no HTML de quem abre: só pra casa ou pra quem tem a senha de dono
            if (api.ScalarAgentKey is { } chave && dono)
            {
                scalar.WithAgentKey(chave);

                if (deDentro)
                    scalar.JavaScriptConfiguration = Módulo;
            }

            // a página de uma versão mostra o documento dela, e só ele
            scalar.Title = $"{Título} {versão}";
            scalar.OpenApiRoutePattern = Documento;
            scalar.AddDocument(versão, $"{Título} {versão}", isDefault: true);

            // em casa a chave fica guardada; em máquina dos outros ela some quando a aba fecha
            scalar.PersistentAuthentication = deDentro;
            scalar.AddPreferredSecuritySchemes([Esquema]);

            // a página é pra uso da casa: sem telemetria do Scalar e sem fonte vinda de CDN
            scalar.Telemetry = false;
            scalar.DefaultFonts = false;

            // a cara é a padrão do Scalar; só sai o que é propaganda dele, não da API
            scalar.HideDeveloperTools();
            scalar.DisableMcp();

            // os exemplos nas linguagens de quem usa este bot: curl, PowerShell, C#, JS e Python
            scalar.EnabledTargets =
                [ScalarTarget.Shell, ScalarTarget.PowerShell, ScalarTarget.CSharp, ScalarTarget.JavaScript, ScalarTarget.Python];
            scalar.WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl);
        });
    }

    // Os endereços de antes da versão: o documento muda com 301, a página com 302.
    private static void MapEndereçosDeAntes(WebApplication app, string versão)
    {
        app.MapGet("/api/openapi.json", () => Results.Redirect(
                Documento.Replace("{documentName}", versão), permanent: true))
            .ExcludeFromDescription();

        // com o {**resto} vale /api/docs, /api/docs/ e o que mais tiver ficado salvo nos favoritos
        app.MapGet("/api/docs/{**resto}", () => Results.Redirect($"{Página}/"))
            .ExcludeFromDescription();
    }

    // O Scalar guarda a autenticação no localStorage; o módulo lê na frente e põe a do bot.
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
