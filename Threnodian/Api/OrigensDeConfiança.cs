namespace AlephBot.Threnodian.Api;

/// <summary>
/// De qual página o navegador pode chamar a API. Antes valia qualquer uma — o que aqui não
/// abre buraco, porque a API é só leitura e não usa cookie nem sessão (nada viaja sozinho
/// junto do pedido, e sem a chave na mão ninguém passa do 401) —, mas era amplo à toa: fora
/// destas, página nenhuma tem o que fazer com as rotas do bot.
///
/// Quem escreve a <c>Origin</c> é o navegador, não a página: site nenhum forja a dele. Pra
/// quem chama de fora de um navegador (curl, o n8n, um script) nada disto vale — ali quem
/// guarda a porta é a chave, como sempre foi.
/// </summary>
internal static class OrigensDeConfiança
{
    /// <summary>Em <c>API_CORS_ORIGINS</c>, volta a liberar qualquer página.</summary>
    public const string Qualquer = "*";

    /// <summary>
    /// <c>.ts.net</c> é a tailnet e o Funnel, de onde a própria página dos docs abre;
    /// <c>.scalar.com</c> é a IA dela, que chama a API de fora da página.
    /// </summary>
    private static readonly string[] Domínios = [".ts.net", ".scalar.com"];

    /// <summary>A máquina de quem desenvolve, o nome curto do bot na tailnet e o Scalar.</summary>
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
