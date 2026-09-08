using AlephBot.Config;
using AlephBot.Core.Personality;

namespace AlephBot.Threnodian;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // só volta daqui quando o host termina de parar: Ctrl+C no terminal,
            // `docker stop` na VPS, ou o próprio bot pedindo pra encerrar
            await new AlephBot(AlephConfig.Load()).RunAsync(args);
        }
        catch (OperationCanceledException)
        {
            // O fim do shutdown cancela o que ainda estava em voo, e o cancelamento sobe
            // até aqui. O caso mais comum é o Lavalink fora do ar: o serviço de áudio
            // segue tentando reconectar, a tentativa é cancelada junto com o encerramento
            // e a TaskCanceledException escapa do host. O bot parou do mesmo jeito — isso
            // é o encerramento terminando, não uma falha.
        }
        catch (Exception erro)
        {
            // O NLog desce junto com o host, então a esta altura o log já pode estar
            // fechado: quem ainda escreve é o console.
            Despedida(Console.Out, erro);
            return 1;
        }

        Despedida(Console.Out, erro: null);
        return 0;
    }

    /// <summary>
    /// O contraponto do banner de boot (Core/Personality/Banner.cs): mesma paleta e mesma
    /// forma, fechando o que ele abriu. Vai direto no console, e não pelo NLog, por dois
    /// motivos: o NLog já desceu junto com o host a esta altura, e despedida com timestamp
    /// e nome de logger na frente perde a graça.
    /// </summary>
    private static void Despedida(TextWriter saida, Exception? erro)
    {
        var cor = Banner.UsaCor();

        var roxo = cor ? Banner.Roxo : "";
        var cinza = cor ? Banner.Cinza : "";
        var vermelho = cor ? Vermelho : "";
        var fim = cor ? Banner.Fim : "";

        var titulo = erro is null ? "desligada" : "caiu";
        var fala = erro is null ? Denia.Despedida() : Denia.DespedidaComErro();

        saida.WriteLine();
        saida.WriteLine($"  {roxo}Denia{fim} {cinza}· {titulo}{fim}");
        saida.WriteLine($"  {cinza}{fala}{fim}");

        // eu engoli a exceção pra poder me despedir; então sou eu que tenho
        // que mostrar o que aconteceu, senão ela some
        if (erro is not null)
        {
            saida.WriteLine();
            saida.WriteLine($"{vermelho}{erro}{fim}");
        }

        saida.WriteLine();
    }

    /// <summary>Mesmo vermelho do nível de erro no nlog.config.</summary>
    private const string Vermelho = "\e[38;5;203m";
}
