using System.Net;

namespace AlephBot.Tests.Api.CaixaPreta;

// Os caminhos de erro: o formato sai das próprias respostas, não de um nome escrito aqui.
public class ErrosTests(CaixaPretaFixture fixture) : IClassFixture<CaixaPretaFixture>
{
    private static readonly IPAddress IpDeFora = IPAddress.Parse("203.0.113.55");

    private readonly ApiDeTeste _api = fixture.Api;
    private readonly DocumentoDaApi _documento = fixture.Documento;

    private IEnumerable<OperaçãoPrometida> ComChave => _documento.Operações.Where(o => o.PedeChave);

    private void ConfereOErro(RespostaPrometida prometida, RespostaCrua resposta, string quando)
    {
        var conferência = _documento.Confere(prometida.Schema!, resposta.Corpo);

        Assert.StartsWith(prometida.TipoDeConteúdo!, resposta.Headers.ContentType.ToString());
        Assert.True(
            conferência.IsValid,
            $"""
            {quando}: o corpo do erro não bate com o formato que o documento promete.

            {DocumentoDaApi.Explica(conferência)}

            O corpo foi: {resposta.Corpo}
            """);
    }

    [Fact]
    public void O_documento_tem_operação_que_pede_chave()
    {
        // senão os dois testes de 401 passariam sem rodar nada
        Assert.NotEmpty(ComChave);
    }

    [Fact]
    public async Task Sem_a_chave_vem_o_401_no_formato_que_o_documento_promete()
    {
        foreach (var operação in ComChave)
        {
            var resposta = await _api.FazAsync(operação.Método, operação.Caminho);
            var prometida = operação.Resposta(401);

            Assert.Equal(401, resposta.Status);
            Assert.True(prometida is not null, $"{operação} recusa sem chave, mas o documento não anuncia o 401");

            ConfereOErro(prometida!, resposta, $"{operação} sem a chave");
        }
    }

    [Fact]
    public async Task O_que_o_documento_marca_como_aberto_responde_sem_chave_nenhuma()
    {
        // a rota que o monitor chama não pode passar a pedir chave sem o documento dizer
        foreach (var operação in _documento.Operações.Where(operação => !operação.PedeChave))
        {
            var resposta = await _api.FazAsync(operação.Método, operação.Caminho);

            Assert.NotEqual(401, resposta.Status);
        }
    }

    [Fact]
    public async Task Com_a_chave_errada_vem_o_mesmo_401()
    {
        foreach (var operação in ComChave)
        {
            var resposta = await _api.FazAsync(
                operação.Método,
                operação.Caminho,
                headers: (_documento.HeaderDaChave, "chave-chutada-1234567890"));

            Assert.Equal(401, resposta.Status);
            ConfereOErro(operação.Resposta(401)!, resposta, $"{operação} com a chave errada");
        }
    }

    [Theory]
    [InlineData("/api/v1/nada")]
    [InlineData("/api/v2/status")]
    public async Task O_endereço_que_não_existe_sai_no_formato_de_erro_do_documento(string caminho)
    {
        // o documento promete 404 em problem+json, inclusive pra versão que não existe
        var resposta = await _api.FazAsync(HttpMethod.Get.Method, caminho);

        Assert.Equal(404, resposta.Status);
        ConfereOErro(_documento.FormatoDeErro, resposta, $"GET {caminho}");
    }

    [Fact]
    public async Task Passar_do_limite_dá_o_429_com_o_corpo_e_os_headers_prometidos()
    {
        // o limite guarda estado, então esta sobe a própria API; de fora a cota é menor
        await using var api = await ApiDeTeste.SubirAsync();
        var documento = await DocumentoDaApi.LerAsync(api);
        var aberta = documento.Operações.First(operação => !operação.PedeChave);

        RespostaCrua resposta;
        var pedidos = 0;

        do
        {
            resposta = await api.FazAsync(aberta.Método, aberta.Caminho, IpDeFora);
        }
        while (resposta.Status != 429 && ++pedidos < 1000);

        var prometida = aberta.Resposta(429);

        Assert.Equal(429, resposta.Status);
        Assert.True(prometida is not null, $"{aberta} recusa por excesso, mas o documento não anuncia o 429");

        var conferência = documento.Confere(prometida!.Schema!, resposta.Corpo);

        Assert.True(conferência.IsValid, $"o 429 não bate com o schema: {DocumentoDaApi.Explica(conferência)}");
        Assert.All(prometida.Headers, header => Assert.True(
            resposta.Headers.ContainsKey(header),
            $"o 429 devia trazer o header {header}, que o documento promete"));
    }
}
