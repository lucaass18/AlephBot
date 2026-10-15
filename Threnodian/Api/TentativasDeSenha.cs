using System.Collections.Concurrent;
using System.Net;

using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Api;

// Quem erra a senha dos docs quatro vezes fica de castigo. Conta por erro, por IP, na memória.
public sealed class TentativasDeSenha(ILogger<TentativasDeSenha> logger)
{
    // Quantos erros até o castigo.
    public const int Limite = 4;

    // Quanto tempo sem poder tentar de novo.
    public static readonly TimeSpan Castigo = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, Conta> _contas = new();

    private sealed record Conta(int Erros, long SoltaEmMs);

    // Quem está contando: o IP, normalizado como o do limite de pedidos.
    public static string ClienteDe(IPAddress? ip) => LimiteDePedidos.ClienteDe(ip);

    // Quanto falta do castigo, ou null. Passado o tempo, a conta é esquecida.
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

    // Acertou: a conta zera, e os erros de antes não contam mais.
    public void Acertou(string cliente) => _contas.TryRemove(cliente, out _);

    // Quem parou de tentar sai da lista; sem isto ela só cresceria.
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
