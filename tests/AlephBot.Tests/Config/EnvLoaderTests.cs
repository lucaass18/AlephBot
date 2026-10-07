using AlephBot.Config;

namespace AlephBot.Tests.Config;

/// <summary>
/// O EnvLoader escreve no ambiente do processo, que é um só pra todos os testes: cada teste
/// usa nomes únicos e apaga o que criou.
/// </summary>
public sealed class EnvLoaderTests : IDisposable
{
    private readonly string _prefixo = $"ALEPH_TESTE_{Guid.NewGuid():N}_";
    private readonly string _arquivo = Path.Combine(Path.GetTempPath(), $"aleph-{Guid.NewGuid():N}.env");
    private readonly List<string> _criadas = [];

    private string Nome(string sufixo)
    {
        var nome = _prefixo + sufixo;
        _criadas.Add(nome);
        return nome;
    }

    private void Carregar(params string[] linhas)
    {
        File.WriteAllLines(_arquivo, linhas);
        EnvLoader.Load(_arquivo);
    }

    public void Dispose()
    {
        foreach (var nome in _criadas)
            Environment.SetEnvironmentVariable(nome, null);

        File.Delete(_arquivo);
    }

    [Fact]
    public void Lê_chave_e_valor()
    {
        var a = Nome("A");

        Carregar($"{a}=valor");

        Assert.Equal("valor", Environment.GetEnvironmentVariable(a));
    }

    [Fact]
    public void Espaço_em_volta_do_igual_e_do_valor_some()
    {
        var a = Nome("A");

        Carregar($"  {a} =  valor  ");

        Assert.Equal("valor", Environment.GetEnvironmentVariable(a));
    }

    [Fact]
    public void Valor_pode_ter_igual_no_meio()
    {
        var a = Nome("A");

        Carregar($"{a}=chave=valor");

        Assert.Equal("chave=valor", Environment.GetEnvironmentVariable(a));
    }

    [Fact]
    public void Aceita_export_na_frente()
    {
        var a = Nome("A");

        Carregar($"export {a}=valor");

        Assert.Equal("valor", Environment.GetEnvironmentVariable(a));
    }

    [Fact]
    public void Tira_aspas_que_envolvem_o_valor()
    {
        var duplas = Nome("DUPLAS");
        var simples = Nome("SIMPLES");
        var meia = Nome("MEIA");

        Carregar($"{duplas}=\"com espaço\"", $"{simples}='simples'", $"{meia}=\"só uma");

        Assert.Equal("com espaço", Environment.GetEnvironmentVariable(duplas));
        Assert.Equal("simples", Environment.GetEnvironmentVariable(simples));
        Assert.Equal("\"só uma", Environment.GetEnvironmentVariable(meia));
    }

    [Fact]
    public void Ignora_comentário_e_linha_sem_chave()
    {
        var comentada = Nome("COMENTADA");
        var válida = Nome("VALIDA");

        Carregar($"# {comentada}=x", "", "linha sem igual", "=sem chave", $"{válida}=ok");

        Assert.Null(Environment.GetEnvironmentVariable(comentada));
        Assert.Equal("ok", Environment.GetEnvironmentVariable(válida));
    }

    [Fact]
    public void Não_sobrescreve_o_que_já_veio_do_ambiente()
    {
        var a = Nome("A");
        Environment.SetEnvironmentVariable(a, "do ambiente");

        Carregar($"{a}=do arquivo");

        Assert.Equal("do ambiente", Environment.GetEnvironmentVariable(a));
    }

    [Fact]
    public void Arquivo_que_não_existe_não_é_erro()
    {
        // em produção as variáveis vêm do contêiner e não há .env nenhum
        var exceção = Record.Exception(() => EnvLoader.Load(_arquivo));
        Assert.Null(exceção);
    }
}
