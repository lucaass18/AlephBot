using System.Security.Cryptography;
using System.Text;

using AlephBot.Config;
using AlephBot.Core.Personality;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Api;

/// <summary>
/// A porta da API: sem a chave certa no header, nada passa. Fica num filtro do grupo /api e
/// não num middleware pra o /api/health, que mora fora do grupo, continuar aberto.
/// </summary>
public sealed class ApiKeyFilter : IEndpointFilter
{
    public const string Header = "X-Api-Key";

    // comparo os hashes, não as chaves: dois SHA-256 têm sempre o mesmo tamanho, então nem o
    // tempo da comparação nem o tamanho da chave contam nada pra quem está chutando
    private readonly byte[] _esperada;
    private readonly ILogger<ApiKeyFilter> _logger;

    public ApiKeyFilter(ApiConfig api, ILogger<ApiKeyFilter> logger)
    {
        _esperada = Hash(api.Key);
        _logger = logger;
    }

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var enviada = context.HttpContext.Request.Headers[Header].ToString();

        if (enviada.Length == 0)
            return Recusa(Denia.ApiSemChave(Header));

        if (!CryptographicOperations.FixedTimeEquals(Hash(enviada), _esperada))
        {
            // pedido sem chave é varredura da internet; chave errada é alguém tentando, e isso eu anoto
            _logger.LogWarning(
                "Chave errada na API: {Método} {Caminho} vindo de {Ip}",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path,
                context.HttpContext.Connection.RemoteIpAddress);

            return Recusa(Denia.ApiChaveErrada());
        }

        return next(context);
    }

    private static ValueTask<object?> Recusa(string motivo) =>
        ValueTask.FromResult<object?>(
            TypedResults.Problem(detail: motivo, statusCode: StatusCodes.Status401Unauthorized));

    private static byte[] Hash(string chave) => SHA256.HashData(Encoding.UTF8.GetBytes(chave));
}
