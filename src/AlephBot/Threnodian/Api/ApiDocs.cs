using System.Text.Json;
using System.Text.RegularExpressions;

using Asp.Versioning;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

using AlephBot.Core.Commands.Registro;

namespace AlephBot.Threnodian.Api;

// O documento OpenAPI, montado das próprias rotas: dele saem a coleção do Postman e as
// asserções da caixa preta.
public static partial class ApiDocs
{
    // /api/v1/openapi.json: um documento por versão, cada um dentro da sua
    private const string Documento = "/api/{documentName}/openapi.json";
    private const string Título = "AlephBot API";

    // o nome com que o documento se refere à chave
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

                // "http://aleph/" vira "http://aleph": com a barra, o endereço sairia dobrado
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

                // a chave vale pro documento inteiro; as rotas abertas desdizem isso, uma a uma
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

    // Cada resposta com o que quer dizer e os headers do limite; sem eles, quem lê o documento
    // não sabe que eles existem.
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
        var atual = Documento.Replace("{documentName}", $"v{AlephApi.VersãoAtual}");

        // /api/v1/openapi.json, e um arquivo novo pra cada versão que nascer
        app.MapOpenApi(Documento).WithDocumentPerVersion();

        // o endereço de antes da versão; 301 porque o documento de cada versão não muda de lugar
        app.MapGet("/api/openapi.json", () => Results.Redirect(atual, permanent: true))
            .ExcludeFromDescription();

        LoginDoDocumento.Map(app);

        // a raiz é a porta da frente: leva pra tela de entrar, e quem já pode ler passa direto
        app.MapGet("/", () => Results.Redirect(LoginDoDocumento.Entrar))
            .ExcludeFromDescription();
    }
}
