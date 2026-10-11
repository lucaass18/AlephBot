namespace AlephBot.Threnodian.Dados;

/// <summary>
/// O que cada servidor escolheu pra si. Tudo aqui é opcional: o que estiver null vale o
/// padrão do bot, que é o que vem do Config/.env. É por isso que a tabela pode estar vazia
/// sem nada quebrar — servidor sem linha é servidor que não mexeu em nada.
/// </summary>
public sealed class ConfigDoServidor
{
    /// <summary>O id do servidor no Discord, que é a chave.</summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// O sinal que chama os comandos de texto deste servidor ("!", "?", "aleph "). Null =
    /// o <c>PREFIX</c> do .env.
    /// </summary>
    public string? Prefixo { get; set; }

    /// <summary>Pra onde mandar aviso de moderação. Null = o bot não avisa em canal nenhum.</summary>
    public ulong? CanalDeLog { get; set; }

    /// <summary>
    /// Quantos minutos o player espera parado antes de sair do canal de voz. Null = o
    /// <c>MUSIC_IDLE_MINUTES</c> do .env.
    /// </summary>
    public int? MinutosDeEspera { get; set; }

    public DateTimeOffset CriadoEm { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }
}
