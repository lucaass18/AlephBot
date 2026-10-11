namespace AlephBot.Tests.Api.CaixaPreta;

/// <summary>
/// O outro lado do contrato: o documento não pode prometer o que a API não faz, nem carregar
/// peso morto. Rota anunciada que não responde, status que sai sem estar anunciado e schema
/// que ninguém alcança são as três formas de a documentação deixar de bater com a realidade.
/// </summary>
public class CoberturaTests(CaixaPretaFixture fixture) : IClassFixture<CaixaPretaFixture>
{
    private readonly ApiDeTeste _api = fixture.Api;
    private readonly DocumentoDaApi _documento = fixture.Documento;

    private Task<RespostaCrua> ChamaAsync(OperaçãoPrometida operação, bool comChave) =>
        _api.FazAsync(
            operação.Método,
            operação.Caminho,
            headers: comChave ? [(_documento.HeaderDaChave, ApiDeTeste.ChaveDaApi)] : []);

    [Fact]
    public async Task Toda_operação_anunciada_responde_de_verdade()
    {
        foreach (var operação in _documento.Operações)
        {
            var resposta = await ChamaAsync(operação, comChave: operação.PedeChave);

            Assert.False(
                resposta.Status is 404 or 405,
                $"o documento anuncia {operação}, mas a API respondeu {resposta.Status}");
        }
    }

    [Fact]
    public async Task Nenhuma_operação_responde_status_que_o_documento_não_anuncia()
    {
        // os dois caminhos que um cliente percorre: com a chave e sem ela
        foreach (var operação in _documento.Operações)
        {
            foreach (var comChave in (bool[])[true, false])
            {
                var resposta = await ChamaAsync(operação, comChave);

                Assert.True(
                    operação.Resposta(resposta.Status) is not null,
                    $"{operação} {(comChave ? "com" : "sem")} a chave respondeu {resposta.Status}, " +
                    "que o documento não anuncia");
            }
        }
    }

    [Fact]
    public void Todo_schema_do_documento_é_alcançável_a_partir_de_alguma_resposta()
    {
        var sobrando = _documento.SchemasDeclarados.Except(_documento.SchemasAlcançáveis).ToList();

        Assert.True(
            sobrando.Count == 0,
            $"o documento declara schema que resposta nenhuma alcança: {string.Join(", ", sobrando)}");
    }

    [Fact]
    public void Cada_operação_tem_um_nome_só_dela()
    {
        // o operationId é o nome pelo qual um cliente gerado chama a rota: repetido, ele colide
        var repetidos = _documento.Operações
            .GroupBy(operação => operação.Id)
            .Where(grupo => grupo.Count() > 1)
            .Select(grupo => grupo.Key);

        Assert.Empty(repetidos);
    }

    [Fact]
    public async Task O_HEAD_responde_o_mesmo_que_o_GET_da_mesma_rota()
    {
        // é o que monitor de uptime chama, e o documento anuncia os mesmos códigos nos dois.
        // Que o HEAD saia sem corpo não dá pra conferir aqui: quem corta o corpo é o Kestrel,
        // e o TestServer não passa por ele
        var pares = from head in _documento.Operações
                    where head.Método == "HEAD"
                    join get in _documento.Operações on (head.Caminho, "GET") equals (get.Caminho, get.Método)
                    select (head, get);

        Assert.NotEmpty(pares);

        foreach (var (head, get) in pares)
        {
            var doHead = await ChamaAsync(head, comChave: head.PedeChave);
            var doGet = await ChamaAsync(get, comChave: get.PedeChave);

            Assert.Equal(doGet.Status, doHead.Status);
        }
    }
}
