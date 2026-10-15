namespace AlephBot.Threnodian.Dados;

// O que cada servidor escolheu. Null vale o padrão do .env, então a tabela pode estar vazia.
public sealed class ConfigDoServidor
{
    // O id do servidor no Discord, que é a chave.
    public ulong GuildId { get; set; }

    // O sinal que chama os comandos de texto ("!", "?", "aleph "). Null = o PREFIX do .env.
    public string? Prefixo { get; set; }

    // Pra onde mandar aviso de moderação. Null = o bot não avisa em canal nenhum.
    public ulong? CanalDeLog { get; set; }

    // Quantos minutos o player espera parado. Null = o MUSIC_IDLE_MINUTES do .env.
    public int? MinutosDeEspera { get; set; }

    public DateTimeOffset CriadoEm { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }
}
