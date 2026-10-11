using AlephBot.Core.Commands.Music;
using AlephBot.Threnodian.Dados;

namespace AlephBot.Tests.Dados;

/// <summary>
/// O que o /playlist aceita e o que ele mostra. A ida e volta entre fila e playlist usa os
/// dados codificados do Lavalink e precisa de um player de verdade pra testar; o que dá
/// pra garantir aqui é o resto — e é o resto que decide se alguém consegue guardar uma
/// playlist e achar ela depois.
/// </summary>
public class PlaylistGuardadaTests
{
    [Theory]
    [InlineData("rock")]
    [InlineData("as do sábado")]
    [InlineData("a")]
    public void Nome_normal_passa(string nome)
    {
        Assert.True(PlaylistGuardada.NomeAceitável(nome));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Nome_vazio_não_passa(string? nome)
    {
        // sem nome ninguém pediria ela de volta
        Assert.False(PlaylistGuardada.NomeAceitável(nome));
    }

    [Fact]
    public void Nome_maior_que_o_banco_aceita_não_passa()
    {
        // o limite é o da coluna: passar daqui estouraria na hora de gravar
        var grande = new string('a', PlaylistSalva.NomeNoMáximo + 1);

        Assert.False(PlaylistGuardada.NomeAceitável(grande));
        Assert.True(PlaylistGuardada.NomeAceitável(new string('a', PlaylistSalva.NomeNoMáximo)));
    }

    [Fact]
    public void O_espaço_das_pontas_não_conta_pro_tamanho()
    {
        var nome = "  " + new string('a', PlaylistSalva.NomeNoMáximo) + "  ";

        Assert.True(PlaylistGuardada.NomeAceitável(nome));
    }

    [Theory]
    [InlineData(PlaylistsSalvas.Resultado.Guardou, null)]
    [InlineData(PlaylistsSalvas.Resultado.NomeRepetido, "já tem")]
    [InlineData(PlaylistsSalvas.Resultado.ServidorCheio, "já tem")]
    [InlineData(PlaylistsSalvas.Resultado.SemBanco, "banco")]
    public void Cada_recusa_explica_o_que_houve(PlaylistsSalvas.Resultado resultado, string? trecho)
    {
        var recusa = PlaylistGuardada.Recusa(resultado, "rock");

        if (trecho is null)
        {
            Assert.Null(recusa);
            return;
        }

        Assert.NotNull(recusa);
        Assert.Contains(trecho, recusa, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_lista_vazia_ensina_a_salvar_a_primeira()
    {
        var embed = PlaylistGuardada.Lista([]);

        Assert.Contains("playlist salvar", embed.Description);
    }

    [Fact]
    public void A_lista_mostra_nome_tamanho_e_quem_salvou()
    {
        var embed = PlaylistGuardada.Lista([Playlist("rock", 3, quem: 42)]);

        Assert.Contains("rock", embed.Description);
        Assert.Contains("3 faixas", embed.Description);
        Assert.Contains("<@42>", embed.Description);
    }

    [Fact]
    public void Uma_faixa_só_não_vira_faixas_no_plural()
    {
        var embed = PlaylistGuardada.Lista([Playlist("solo", 1, quem: 1)]);

        Assert.Contains("1 faixa,", embed.Description);
    }

    [Fact]
    public void A_lista_longa_diz_quantas_ficaram_de_fora()
    {
        var muitas = Enumerable.Range(1, 14).Select(i => Playlist($"p{i}", 1, quem: 1)).ToList();

        var embed = PlaylistGuardada.Lista(muitas);

        Assert.Contains("(14)", embed.Title);
        Assert.Contains("e mais 4", embed.Description);
    }

    [Fact]
    public void Quando_faixa_não_toca_mais_o_aviso_conta_quantas()
    {
        // a playlist tinha 10, só 7 voltaram: as outras 3 são link que morreu
        var embed = PlaylistGuardada.Enfileirou(Playlist("antiga", 10, quem: 1), quantas: 7);

        Assert.Contains("7 faixas", embed.Description);
        Assert.Contains("3 faixas não tocam mais", embed.Description);
    }

    [Fact]
    public void Com_tudo_tocando_não_sobra_aviso_nenhum()
    {
        var embed = PlaylistGuardada.Enfileirou(Playlist("nova", 5, quem: 1), quantas: 5);

        Assert.DoesNotContain("não tocam mais", embed.Description);
    }

    [Fact]
    public void Ao_guardar_ele_já_diz_como_tocar_de_volta()
    {
        var embed = PlaylistGuardada.Guardou("sábado", 12);

        Assert.Contains("12 faixas", embed.Description);
        Assert.Contains("/playlist tocar sábado", embed.Description);
    }

    private static PlaylistSalva Playlist(string nome, int faixas, ulong quem) => new()
    {
        Nome = nome,
        CriadaPor = quem,
        Faixas = [.. Enumerable.Range(0, faixas).Select(i => new FaixaDaPlaylist { Ordem = i })],
    };
}
