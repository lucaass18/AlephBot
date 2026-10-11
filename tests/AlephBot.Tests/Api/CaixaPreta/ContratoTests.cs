namespace AlephBot.Tests.Api.CaixaPreta;

/// <summary>
/// O contrato: pra cada operação que o documento anuncia, a resposta de verdade é conferida
/// contra o que ele promete — o schema do corpo, o tipo do conteúdo e os headers. Nada aqui
/// sabe o nome de um record do bot; tudo sai do /api/v1/openapi.json.
///
/// É o cruzamento que faltava. O <see cref="ApiDocsTests"/> confere o que o documento diz, e o
/// <see cref="ApiRoutesTests"/> confere o que as rotas respondem — mas os dois em separado:
/// documento e resposta podiam divergir sem quebrar nada.
/// </summary>
public class ContratoTests(CaixaPretaFixture fixture) : IClassFixture<CaixaPretaFixture>
{
    private readonly ApiDeTeste _api = fixture.Api;
    private readonly DocumentoDaApi _documento = fixture.Documento;

    /// <summary>Chama a operação do jeito que o documento manda: com a chave, se ela pedir.</summary>
    private Task<RespostaCrua> ChamaAsync(OperaçãoPrometida operação) =>
        _api.FazAsync(
            operação.Método,
            operação.Caminho,
            headers: operação.PedeChave ? [(_documento.HeaderDaChave, ApiDeTeste.ChaveDaApi)] : []);

    /// <summary>A resposta que o documento promete pro código que saiu — e ela tem que existir.</summary>
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

            // resposta sem corpo não tem tipo a conferir: o HEAD repete os headers do GET, e
            // ter Content-Type ali é o HTTP mandando, não o documento sendo desobedecido
            if (prometida.TipoDeConteúdo is null)
                continue;

            Assert.StartsWith(prometida.TipoDeConteúdo, resposta.Headers.ContentType.ToString());
        }
    }

    [Fact]
    public async Task Toda_resposta_traz_os_headers_que_o_documento_promete()
    {
        // o documento promete a cota e a sobra em toda resposta; até aqui ninguém conferia se
        // elas chegam de verdade
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
