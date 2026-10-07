namespace AlephBot.Core.Personality;

/// <summary>
/// O cartão de visita que aparece no boot, antes do primeiro log.
/// Vai direto no console (não pelo NLog) pra não sair picado com timestamp em cada linha.
/// </summary>
public static class Banner
{
    private const string Arte =
        """
        ▄▀█ █░░ █▀▀ █▀█ █░█   █▄▄ █▀█ ▀█▀
        █▀█ █▄▄ ██▄ █▀▀ █▀█   █▄█ █▄█ ░█░
        """;

    // a paleta do console: os mesmos códigos do nlog.config, pra banner, log, despedida e o
    // relatório dos testes falarem na mesma cor. Roxo é o Info (deu certo), laranja o Warn,
    // vermelho o Error; cinza e apagado são o que acompanha
    public const string Roxo = "\e[38;5;141m";
    public const string Cinza = "\e[38;5;245m";
    public const string Apagado = "\e[38;5;240m";
    public const string Laranja = "\e[38;5;214m";
    public const string Vermelho = "\e[38;5;203m";
    public const string Fim = "\e[0m";

    public static void Print(TextWriter saida, string prefixo, string modo, string versao, string runtime)
    {
        var cor = UsaCor();

        var roxo = cor ? Roxo : "";
        var cinza = cor ? Cinza : "";
        var fim = cor ? Fim : "";

        saida.WriteLine();

        foreach (var linha in Arte.Split('\n'))
            saida.WriteLine($"  {roxo}{linha.TrimEnd()}{fim}");

        saida.WriteLine();
        saida.WriteLine($"  {roxo}Denia{fim} {cinza}· Bubbles of Nihility{fim}");
        saida.WriteLine($"  {cinza}{Denia.Assinatura}{fim}");
        saida.WriteLine();
        saida.WriteLine($"  {cinza}prefixo {fim}{prefixo}{cinza}  ·  {fim}{modo}{cinza}  ·  v{versao}  ·  {runtime}{fim}");
        saida.WriteLine();
    }

    /// <summary>
    /// Sem cor quando a saída é redirecionada (arquivo, `docker logs`, pipe) ou quando
    /// o ambiente pede NO_COLOR — senão o log vira sopa de escape.
    /// </summary>
    public static bool UsaCor() =>
        !Console.IsOutputRedirected
        && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
}
