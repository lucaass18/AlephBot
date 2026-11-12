namespace AlephBot.Threnodian.Dados;

// Uma foto dos números, de tempos em tempos. Os nomes são os que a tabela do n8n usava.
public sealed class ColetaDeStats
{
    public long Id { get; set; }

    public DateTimeOffset ColetadoEm { get; set; }

    public int Servidores { get; set; }

    // Soma os membros de cada servidor: quem está em dois conta duas vezes.
    public int Membros { get; set; }

    // Pessoas distintas online, sem bots.
    public int Online { get; set; }

    public int Comandos { get; set; }

    public int Players { get; set; }

    public int Tocando { get; set; }

    public int Pausados { get; set; }

    public int NaFila { get; set; }

    public double MemoriaMb { get; set; }

    public double HeapMb { get; set; }
}
