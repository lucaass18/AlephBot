namespace AlephBot.Threnodian.Rede;

// Meus HttpClient vivem o processo inteiro, o que é certo pro socket e errado pro DNS: sem prazo
// na conexão, o pool fica preso ao IP de quando o bot subiu. AniList e MangaDex estão atrás de
// Cloudflare, que troca de IP, e numa VPS de semanas isso aparece como "parou do nada".
internal static class ClienteHttp
{
    // O prazo que a documentação do .NET recomenda pra cliente que não é descartado.
    private static readonly TimeSpan VidaDaConexão = TimeSpan.FromMinutes(5);

    public static HttpClient Novo(TimeSpan espera, Uri? endereço = null) =>
        new(new SocketsHttpHandler { PooledConnectionLifetime = VidaDaConexão })
        {
            BaseAddress = endereço,
            Timeout = espera,
        };
}
