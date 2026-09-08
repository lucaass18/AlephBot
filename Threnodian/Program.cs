using AlephBot.Config;
using AlephBot.Core.Personality;

namespace AlephBot.Threnodian;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // só volta daqui quando o host termina de parar: Ctrl+C no terminal,
        // `docker stop` na VPS, ou o próprio bot pedindo pra encerrar
        await new AlephBot(AlephConfig.Load()).RunAsync(args);

        Despedida(Console.Out);
    }

    /// <summary>
    /// O contraponto do banner de boot (Core/Personality/Banner.cs): mesma paleta e mesma
    /// forma, fechando o que ele abriu. Vai direto no console, e não pelo NLog, por dois
    /// motivos: o NLog já desceu junto com o host a esta altura, e despedida com timestamp
    /// e nome de logger na frente perde a graça.
    /// </summary>
    private static void Despedida(TextWriter saida)
    {
        var cor = Banner.UsaCor();

        var roxo = cor ? Banner.Roxo : "";
        var cinza = cor ? Banner.Cinza : "";
        var fim = cor ? Banner.Fim : "";

        saida.WriteLine();
        saida.WriteLine($"  {roxo}Denia{fim} {cinza}· desligada{fim}");
        saida.WriteLine($"  {cinza}{Denia.Despedida()}{fim}");
        saida.WriteLine();
    }
}
