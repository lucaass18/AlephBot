namespace AlephBot.Tests.Api.CaixaPreta;

// O contrato: cada operação do documento conferida contra a resposta de verdade, sem saber nomes.
public class ContratoTests(CaixaPretaFixture fixture) : IClassFixture<CaixaPretaFixture>
{
    private readonly ApiDeTeste _api = fixture.Api;
    private readonly DocumentoDaApi _documento = fixture.Documento;

    // Chama a operação do jeito que o documento manda: com a chave, se ela pedir.
    private Task<RespostaCrua> ChamaAsync(OperaçãoPrometida operação) =>
        _api.FazAsync(
            operação.Método,
            operação.Caminho,
            headers: operação.PedeChave ? [(_documento.HeaderDaChave, ApiDeTeste.ChaveDaApi)] : []);

    // A resposta que o documento promete pro código que saiu — e ela tem que existir.
    private static RespostaPrometida Prometida(OperaçãoPrometida operação, RespostaCrua resposta)
    {
        var prometida = operação.Resposta(resposta.Status);

        Assert.True(
            prometida is not null,
            $"{operação} respondeu {resposta.Status}, que o documento não prevê — a documentação mente por omissão");

        return prometida!;
    }

    [Fact]
    public void O_documento_anuncia_alguma_operação()
    {
        // se a leitura do documento quebrar, os outros testes passariam vazios sem ninguém ver
        Assert.NotEmpty(_documento.Operações);
        Assert.All(_documento.Operações, operação => Assert.NotEmpty(operação.Respostas));
    }

    [Fact]
    public async Task Toda_resposta_valida_contra_o_schema_que_o_documento_promete()
    {
        foreach (var operação in _documento.Operações)
        {
            var resposta = await ChamaAsync(operação);
            var prometida = Prometida(operação, resposta);

            if (!prometida.TemCorpo)
                continue;

            var conferência = _documento.Confere(prometida.Schema!, resposta.Corpo);

            Assert.True(
                conferência.IsValid,
                $"""
                {operação} respondeu {resposta.Status} com um corpo que não bate com o schema do documento.

                {DocumentoDaApi.Explica(conferência)}

                O corpo foi: {resposta.Corpo}
                """);
        }
    }

    [Fact]
    public async Task Toda_resposta_sai_no_tipo_de_conteúdo_que_o_documento_declara()
    {
        foreach (var operação in _documento.Operações)
        {
            var resposta = await ChamaAsync(operação);
            var prometida = Prometida(operação, resposta);

            // resposta sem corpo não tem tipo: Content-Type no HEAD é o HTTP, não o documento
            if (prometida.TipoDeConteúdo is null)
                continue;

            Assert.StartsWith(prometida.TipoDeConteúdo, resposta.Headers.ContentType.ToString());
        }
    }

    [Fact]
    public async Task Toda_resposta_traz_os_headers_que_o_documento_promete()
    {
        // o documento promete a cota e a sobra em toda resposta, e ninguém conferia se chegam
        foreach (var operação in _documento.Operações)
        {
            var resposta = await ChamaAsync(operação);
            var prometida = Prometida(operação, resposta);

            Assert.All(prometida.Headers, header => Assert.True(
                resposta.Headers.ContainsKey(header),
                $"{operação} ({resposta.Status}) devia trazer o header {header}, que o documento promete"));
        }
    }

}
