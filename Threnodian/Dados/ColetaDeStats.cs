namespace AlephBot.Threnodian.Dados;

/// <summary>
/// Uma foto dos números do bot, tirada de tempos em tempos. É o que desenha a linha do
/// tempo do painel: sozinha não diz nada, em série conta se o bot está crescendo, se a
/// memória está subindo devagar e a que horas as pessoas aparecem.
///
/// Os nomes são os mesmos que a tabela do n8n já usava, pra que o histórico de antes e o
/// de agora sejam a mesma coluna — e não duas que quase combinam.
/// </summary>
public sealed class ColetaDeStats
{
    public long Id { get; set; }

    public DateTimeOffset ColetadoEm { get; set; }

    public int Servidores { get; set; }

    /// <summary>Soma os membros de cada servidor: quem está em dois conta duas vezes.</summary>
    public int Membros { get; set; }

    /// <summary>Pessoas distintas online, sem bots.</summary>
    public int Online { get; set; }

    public int Comandos { get; set; }

    public int Players { get; set; }

    public int Tocando { get; set; }

    public int Pausados { get; set; }

    public int NaFila { get; set; }

    public double MemoriaMb { get; set; }

    public double HeapMb { get; set; }
}
