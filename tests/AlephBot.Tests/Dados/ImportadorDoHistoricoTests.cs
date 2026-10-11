using AlephBot.Threnodian.Dados;

namespace AlephBot.Tests.Dados;

/// <summary>
/// A leitura do histórico exportado do n8n. É o único dado do bot que não se refaz:
/// configuração a pessoa digita de novo, playlist se monta outra vez, mas série temporal
/// só existe porque alguém coletou semana após semana. Ler errado aqui é perder isso.
/// </summary>
public class ImportadorDoHistoricoTests
{
    private const string UmaLinha = """
        [
          {
            "coletado_em": "2026-10-01T12:00:00Z",
            "servidores": 12, "membros": 3456, "online": 210, "comandos": 21,
            "players": 2, "tocando": 1, "pausados": 1, "na_fila": 17,
            "memoria_mb": 142.7, "heap_mb": 38.1
          }
        ]
        """;

    [Fact]
    public void Lê_a_lista_com_os_nomes_das_colunas_do_n8n()
    {
        var linha = Assert.Single(ImportadorDoHistorico.Lê(UmaLinha));

        Assert.Equal(DateTimeOffset.Parse("2026-10-01T12:00:00Z"), linha.ColetadoEm);
        Assert.Equal(12, linha.Servidores);
        Assert.Equal(3456, linha.Membros);
        Assert.Equal(210, linha.Online);
        Assert.Equal(17, linha.NaFila);
        Assert.Equal(142.7, linha.MemoriaMb);
        Assert.Equal(38.1, linha.HeapMb);
    }

    [Fact]
    public void Aceita_o_formato_embrulhado_em_data()
    {
        // dependendo de como a tabela for exportada, a lista vem dentro de um objeto
        var linha = Assert.Single(ImportadorDoHistorico.Lê($$"""{"data": {{UmaLinha}} }"""));

        Assert.Equal(12, linha.Servidores);
    }

    [Fact]
    public void O_backup_das_3h_volta_a_ser_lido()
    {
        // o arquivo que o fluxo guarda toda noite tem que servir pra restaurar; senão é
        // arquivo, não cópia de segurança
        var backup = $$"""
            {
              "salvoEm": "2026-10-11T06:00:00Z",
              "coletas": 1,
              "servidores": 0,
              "config": [],
              "historico": {{UmaLinha}}
            }
            """;

        var linha = Assert.Single(ImportadorDoHistorico.Lê(backup));

        Assert.Equal(12, linha.Servidores);
        Assert.Equal(3456, linha.Membros);
    }

    [Fact]
    public void Aceita_número_que_veio_como_texto()
    {
        // exportação que passou por CSV transforma tudo em string pelo caminho
        var json = """
            [{"coletado_em":"2026-10-01T12:00:00Z","servidores":"12","membros":"3456",
              "online":"210","comandos":"21","players":"0","tocando":"0","pausados":"0",
              "na_fila":"0","memoria_mb":"142.7","heap_mb":"38.1"}]
            """;

        var linha = Assert.Single(ImportadorDoHistorico.Lê(json));

        Assert.Equal(12, linha.Servidores);
        Assert.Equal(142.7, linha.MemoriaMb);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"data": []}""")]
    [InlineData("\"nada\"")]
    public void Arquivo_sem_coleta_nenhuma_devolve_vazio_em_vez_de_estourar(string json)
    {
        Assert.Empty(ImportadorDoHistorico.Lê(json));
    }

    [Fact]
    public void Lê_várias_coletas_na_ordem_em_que_vieram()
    {
        var json = """
            [
              {"coletado_em":"2026-10-01T12:00:00Z","servidores":1,"membros":1,"online":1,
               "comandos":1,"players":0,"tocando":0,"pausados":0,"na_fila":0,
               "memoria_mb":1.0,"heap_mb":1.0},
              {"coletado_em":"2026-10-01T12:15:00Z","servidores":2,"membros":2,"online":2,
               "comandos":1,"players":0,"tocando":0,"pausados":0,"na_fila":0,
               "memoria_mb":2.0,"heap_mb":2.0}
            ]
            """;

        var linhas = ImportadorDoHistorico.Lê(json);

        Assert.Equal(2, linhas.Count);
        Assert.Equal(1, linhas[0].Servidores);
        Assert.Equal(2, linhas[1].Servidores);
    }

    [Fact]
    public void Json_quebrado_estoura_pra_quem_chamou_decidir()
    {
        // o chamador registra no log e deixa o arquivo onde está, em vez de renomear e
        // perder o que não conseguiu ler
        Assert.ThrowsAny<Exception>(() => ImportadorDoHistorico.Lê("{isto não é json"));
    }
}
