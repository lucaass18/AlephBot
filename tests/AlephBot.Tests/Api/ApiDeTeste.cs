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

// A API de verdade num TestServer: as mesmas rotas, sem porta, sem Discord e sem Lavalink.
public sealed class ApiDeTeste : IAsyncDisposable
{
    public const string ChaveDaApi = "chave-da-api-de-teste-123";

    // montado na hora porque, escrito inteiro, o scanner de segredos do GitHub barra o push
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
        IReadOnlyDictionary<string, string>? variáveis = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        var config = AlephConfig.De(nome => nome switch
        {
            "TOKEN" => TokenDeMentira,
            "API_KEY" => ChaveDaApi,
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

    // Um pedido com IP e headers escolhidos: é pelo IP que a API decide o que entregar.
    public async Task<(int Status, string Corpo)> GetAsync(
        string caminho,
        IPAddress? ip = null,
        string? host = null,
        params (string Nome, string Valor)[] headers)
    {
        var (status, corpo, _) = await GetComHeadersAsync(caminho, ip, host, headers);
        return (status, corpo);
    }

    // O mesmo pedido, devolvendo também os headers da resposta.
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

// Um IAudioService que não faz nada mas responde o bastante: o /stats lê Players.Players.
public class ÁudioDeMentira : DispatchProxy
{
    public static IAudioService Criar() => Create<IAudioService, ÁudioDeMentira>();

    protected override object? Invoke(MethodInfo? método, object?[]? argumentos)
    {
        if (método?.ReturnType is not { } tipo || tipo == typeof(void))
            return null;

        // coleção vem vazia e interface vira outro proxy, pra cadeia não parar num null
        if (ItemDaColeção(tipo) is { } item)
            return Array.CreateInstance(item, 0);

        if (tipo.IsInterface)
            return Proxy(tipo);

        return tipo.IsValueType ? Activator.CreateInstance(tipo) : null;
    }

    private static Type? ItemDaColeção(Type tipo) =>
        tipo is { IsInterface: true, IsGenericType: true } && tipo.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? tipo.GetGenericArguments()[0]
            : null;

    // o Create tem irmãos de mesmo nome: é o genérico de dois tipos e nenhum argumento
    private static readonly MethodInfo CriaProxy = typeof(DispatchProxy)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(método => método.Name == nameof(Create)
            && método.IsGenericMethodDefinition
            && método.GetGenericArguments().Length == 2
            && método.GetParameters().Length == 0);

    private static object Proxy(Type tipo) =>
        CriaProxy.MakeGenericMethod(tipo, typeof(ÁudioDeMentira)).Invoke(null, null)!;
}
