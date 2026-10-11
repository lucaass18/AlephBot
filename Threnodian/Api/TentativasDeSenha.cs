using System.Collections.Concurrent;
using System.Net;

using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Api;

/// <summary>
/// Quem erra a senha dos docs quatro vezes fica de castigo por um tempo. O limite de pedidos
/// já segura o volume (60 por minuto de fora), mas 60 chutes por minuto, hora após hora, dão
/// muitos chutes; aqui a conta é por erro, e não por pedido, então quem sabe a senha nunca
/// esbarra nisto.
///
/// A conta é por IP, do mesmo jeito que a cota (IPv6 pelo /64, senão trocar de endereço dentro
/// da casa zeraria o castigo de graça). Mora na memória e morre com o contêiner: castigo de
/// quinze minutos não vale um banco.
/// </summary>
public sealed class TentativasDeSenha(ILogger<TentativasDeSenha> logger)
{
    /// <summary>Quantos erros até o castigo.</summary>
    public const int Limite = 4;

    /// <summary>Quanto tempo sem poder tentar de novo.</summary>
    public static readonly TimeSpan Castigo = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, Conta> _contas = new();

    private sealed record Conta(int Erros, long SoltaEmMs);

    /// <summary>Quem está contando: o IP, normalizado como o do limite de pedidos.</summary>
    public static string ClienteDe(IPAddress? ip) => LimiteDePedidos.ClienteDe(ip);

    /// <summary>
    /// Quanto ainda falta do castigo, ou null se a pessoa pode tentar. Passado o tempo, a conta
    /// é esquecida e ela recomeça do zero.
    /// </summary>
    public TimeSpan? Falta(string cliente, long agoraEmMs)
    {
        if (!_contas.TryGetValue(cliente, out var conta) || conta.Erros < Limite)
            return null;

        var resto = conta.SoltaEmMs - agoraEmMs;

        if (resto > 0)
            return TimeSpan.FromMilliseconds(resto);

        _contas.TryRemove(cliente, out _);
        return null;
    }

    public void Errou(string cliente, long agoraEmMs)
    {
        var solta = agoraEmMs + (long)Castigo.TotalMilliseconds;

        var conta = _contas.AddOrUpdate(
            cliente,
            _ => new Conta(1, solta),
            (_, velha) => velha.Erros >= Limite && velha.SoltaEmMs > agoraEmMs
                ? velha // já está de castigo: não empurra o fim pra frente a cada batida na porta
                : new Conta(velha.SoltaEmMs > agoraEmMs ? velha.Erros + 1 : 1, solta));

        if (conta.Erros == Limite)
        {
            logger.LogWarning(
                "Senha dos docs errada {Erros} vezes vindo de {Cliente}: sem novas tentativas por {Minutos} min",
                conta.Erros,
                cliente,
                (int)Castigo.TotalMinutes);
        }

        Limpa(agoraEmMs);
    }

    /// <summary>Acertou: a conta zera, e os erros de antes não contam mais.</summary>
    public void Acertou(string cliente) => _contas.TryRemove(cliente, out _);

    /// <summary>Quem parou de tentar sai da lista; sem isto ela só cresceria.</summary>
    private void Limpa(long agoraEmMs)
    {
        if (_contas.Count <= 1024)
            return;

        foreach (var (cliente, conta) in _contas)
        {
            if (conta.SoltaEmMs <= agoraEmMs)
                _contas.TryRemove(cliente, out _);
        }
    }
}
