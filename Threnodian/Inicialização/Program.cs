using AlephBot.Config;
using AlephBot.Core.Personality;

namespace AlephBot.Threnodian.Inicialização;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // fico aqui até o host parar de vez: Ctrl+C, docker stop, ou eu mesma pedindo
            await new AlephBot(AlephConfig.Load()).RunAsync(args);
        }
        catch (OperationCanceledException)
        {
            // com o Lavalink fora, a reconexão morre no shutdown: é exceção, mas eu parei certinho
        }
        catch (Exception erro)
        {
            // o NLog dormiu junto com o host; quem ainda me escuta é o console
            Despedida(Console.Out, erro);
            return 1;
        }

        Despedida(Console.Out, erro: null);
        return 0;
    }

    // Fecha o que o banner abriu, na mesma paleta. No console: com timestamp perde a graça.
    private static void Despedida(TextWriter saida, Exception? erro)
    {
        var cor = Banner.UsaCor();

        var roxo = cor ? Banner.Roxo : "";
        var cinza = cor ? Banner.Cinza : "";
        var vermelho = cor ? Banner.Vermelho : "";
        var fim = cor ? Banner.Fim : "";

        var titulo = erro is null ? "desligada" : "caiu";
        var fala = erro is null ? Denia.Despedida() : Denia.DespedidaComErro();

        saida.WriteLine();
        saida.WriteLine($"  {roxo}Denia{fim} {cinza}· {titulo}{fim}");
        saida.WriteLine($"  {cinza}{fala}{fim}");

        // engoli a exceção pra conseguir me despedir, então mostro ela aqui — senão some
        if (erro is not null)
        {
            saida.WriteLine();
            saida.WriteLine($"{vermelho}{erro}{fim}");
        }

        saida.WriteLine();
    }
}
