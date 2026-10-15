namespace AlephBot.Threnodian.Api;

// De qual página o navegador pode chamar a API. Pra quem não é navegador, quem guarda é a chave.
internal static class OrigensDeConfiança
{
    // Em API_CORS_ORIGINS, volta a liberar qualquer página.
    public const string Qualquer = "*";

    // ts.net é a tailnet e o Funnel; scalar.com é a IA dos docs, que chama de fora da página.
    private static readonly string[] Domínios = [".ts.net", ".scalar.com"];

    // A máquina de quem desenvolve, o nome curto do bot na tailnet e o Scalar.
    private static readonly string[] Nomes = ["localhost", "127.0.0.1", "::1", "aleph", "scalar.com"];

    public static bool Confia(string origem, IReadOnlyList<string> extras)
    {
        if (extras.Contains(origem, StringComparer.OrdinalIgnoreCase))
            return true;

        // origem que não é endereço não é de ninguém
        if (!Uri.TryCreate(origem, UriKind.Absolute, out var endereço))
            return false;

        return Nomes.Contains(endereço.Host, StringComparer.OrdinalIgnoreCase)
            || Domínios.Any(domínio => endereço.Host.EndsWith(domínio, StringComparison.OrdinalIgnoreCase));
    }
}
