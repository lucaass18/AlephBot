using System.Net;

using Microsoft.AspNetCore.Http;

namespace AlephBot.Tests.Api.CaixaPreta;

/// <summary>O que um cliente de fora vê de uma resposta: o código, o corpo e os headers.</summary>
public sealed record RespostaCrua(int Status, string Corpo, IHeaderDictionary Headers);

internal static class Chamada
{
    /// <summary>
    /// Um pedido em qualquer método. O <see cref="ApiDeTeste.GetAsync"/> só faz GET, e a caixa
    /// preta chama o que estiver no documento — inclusive o HEAD que o health anuncia.
    /// </summary>
    public static async Task<RespostaCrua> FazAsync(
        this ApiDeTeste api,
        string método,
        string caminho,
        IPAddress? ip = null,
        params (string Nome, string Valor)[] headers)
    {
        var contexto = await api.Servidor.SendAsync(http =>
        {
            http.Request.Method = método;
            http.Request.Path = caminho;
            http.Connection.RemoteIpAddress = ip ?? IPAddress.Loopback;

            foreach (var (nome, valor) in headers)
                http.Request.Headers[nome] = valor;
        });

        using var leitor = new StreamReader(contexto.Response.Body);
        return new RespostaCrua(contexto.Response.StatusCode, await leitor.ReadToEndAsync(), contexto.Response.Headers);
    }
}

/// <summary>
/// Uma API e o documento dela por classe de teste. Cada classe ganha a sua: o limite de
/// pedidos guarda estado, e um teste não pode gastar a cota do outro.
/// </summary>
public sealed class CaixaPretaFixture : IAsyncLifetime
{
    public ApiDeTeste Api { get; private set; } = null!;

    public DocumentoDaApi Documento { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Api = await ApiDeTeste.SubirAsync();
        Documento = await DocumentoDaApi.LerAsync(Api);
    }

    public ValueTask DisposeAsync() => Api.DisposeAsync();
}
