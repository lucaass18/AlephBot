using System.Net;
using System.Reflection;

using AlephBot.Config;
using AlephBot.Core.Commands;
using AlephBot.Threnodian.Api;

using Lavalink4NET;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NetCord;
using NetCord.Gateway;

namespace AlephBot.Tests.Api;

/// <summary>
/// A API de verdade, num TestServer em memória: as mesmas rotas, filtros e documentação do
/// bot, sem porta aberta, sem Discord e sem Lavalink. O gateway nasce sem conectar, e o
/// áudio é um proxy que só aceita a inscrição nos eventos.
/// </summary>
public sealed class ApiDeTeste : IAsyncDisposable
{
    public const string ChaveDaApi = "chave-da-api-de-teste-123";
    public const string ChaveDoAgent = "agent-key-de-teste";

    // formato de token do Discord: o id do bot em base64 e mais duas partes que ninguém confere
    // aqui. Montado na hora de propósito: escrito inteiro no código, o scanner de segredos do
    // GitHub acha que é um token de verdade e barra o push
    private static readonly string TokenDeMentira =
        $"{Convert.ToBase64String("123456789012345678"u8)}.teste.teste";

    private readonly WebApplication _app;

    private ApiDeTeste(WebApplication app)
    {
        _app = app;
        Servidor = app.GetTestServer();
    }

    public TestServer Servidor { get; }

    public static async Task<ApiDeTeste> SubirAsync(
        string? chaveDoAgent = ChaveDoAgent,
        string? senhaDosDocs = null,
        IReadOnlyDictionary<string, string>? variáveis = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        var config = AlephConfig.De(nome => nome switch
        {
            "TOKEN" => TokenDeMentira,
            "API_KEY" => ChaveDaApi,
            "SCALAR_AGENT_KEY" => chaveDoAgent,
            "API_DOCS_PASSWORD" => senhaDosDocs,
            _ => variáveis?.GetValueOrDefault(nome),
        });

        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton(new GatewayClient(new BotToken(TokenDeMentira)));
        builder.Services.AddSingleton(ÁudioDeMentira.Criar());
        builder.Services.AddSingleton<CommandRegistry>();

        AlephApi.Configure(builder, config.Api!);

        var app = builder.Build();
        AlephApi.Map(app);
        await app.StartAsync();

        return new ApiDeTeste(app);
    }

    /// <summary>
    /// Um pedido com o IP de origem e os headers escolhidos — o TestServer não inventa IP, e
    /// é pelo IP (e pela marca do Funnel) que a API decide o que entregar.
    /// </summary>
    public async Task<(int Status, string Corpo)> GetAsync(
        string caminho,
        IPAddress? ip = null,
        string? host = null,
        params (string Nome, string Valor)[] headers)
    {
        var (status, corpo, _) = await GetComHeadersAsync(caminho, ip, host, headers);
        return (status, corpo);
    }

    /// <summary>O mesmo pedido, devolvendo também os headers da resposta.</summary>
    public async Task<(int Status, string Corpo, IHeaderDictionary Headers)> GetComHeadersAsync(
        string caminho,
        IPAddress? ip = null,
        string? host = null,
        params (string Nome, string Valor)[] headers)
    {
        var contexto = await Servidor.SendAsync(http =>
        {
            http.Request.Method = HttpMethods.Get;
            http.Request.Path = caminho;
            http.Connection.RemoteIpAddress = ip ?? IPAddress.Loopback;

            if (host is not null)
                http.Request.Host = new HostString(host);

            foreach (var (nome, valor) in headers)
                http.Request.Headers[nome] = valor;
        });

        using var leitor = new StreamReader(contexto.Response.Body);
        return (contexto.Response.StatusCode, await leitor.ReadToEndAsync(), contexto.Response.Headers);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

/// <summary>
/// Um IAudioService que não faz nada. O LavalinkMonitor só se inscreve nos eventos dele no
/// construtor, e o proxy aceita a inscrição em silêncio.
/// </summary>
public class ÁudioDeMentira : DispatchProxy
{
    public static IAudioService Criar() => Create<IAudioService, ÁudioDeMentira>();

    protected override object? Invoke(MethodInfo? método, object?[]? argumentos) => null;
}
