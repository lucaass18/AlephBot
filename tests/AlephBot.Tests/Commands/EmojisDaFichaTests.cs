using AlephBot.Core.Personality;
using AlephBot.Threnodian.Emojis;
using AlephBot.Threnodian.MyAnimeList;

namespace AlephBot.Tests.Commands;

// Os rótulos da ficha com o emoji do app. O mapa é do processo, então cada teste limpa o que pôs.
[Collection("emoji")]
public class EmojisDaFichaTests : IDisposable
{
    public void Dispose() => EmojisDaFicha.Esquece();

    // Sem emoji subido o bot não pode quebrar: o rótulo vira a palavra, como era antes.
    [Fact]
    public void Sem_emoji_o_rótulo_é_só_a_palavra()
    {
        EmojisDaFicha.Esquece();

        Assert.Equal("Nota", Denia.MalCampoNota);
        Assert.Equal("Exibição", Denia.MalCampoPeríodo(TipoDeObra.Anime));
        Assert.Null(EmojisDaFicha.Marcação("ma_nota"));
    }

    [Fact]
    public void Com_emoji_subido_o_rótulo_ganha_a_marcação()
    {
        EmojisDaFicha.Esquece();
        EmojisDaFicha.Guarda("ma_nota", "1234567890", animado: false);

        Assert.Equal("<:ma_nota:1234567890> Nota", Denia.MalCampoNota);
    }

    // Emoji animado tem o "a:" na frente; sem isso o Discord mostra o texto cru.
    [Fact]
    public void Emoji_animado_leva_o_a_na_frente()
    {
        EmojisDaFicha.Esquece();
        EmojisDaFicha.Guarda("ma_ranking", "42", animado: true);

        Assert.Equal("<a:ma_ranking:42> Ranking", Denia.MalCampoRanking);
    }

    // Faltar um não derruba os outros: cada rótulo decide sozinho.
    [Fact]
    public void Emoji_que_falta_não_atrapalha_quem_existe()
    {
        EmojisDaFicha.Esquece();
        EmojisDaFicha.Guarda("ma_generos", "7", animado: false);

        Assert.Equal("<:ma_generos:7> Gêneros", Denia.MalCampoGêneros);
        Assert.Equal("Elenco", Denia.MalCampoElenco);
    }

    // Estúdio e autoria são emoji diferentes, porque anime e mangá creditam coisas diferentes.
    [Fact]
    public void Anime_e_mangá_usam_emoji_diferentes_na_autoria()
    {
        EmojisDaFicha.Esquece();
        EmojisDaFicha.Guarda("ma_estudio", "1", animado: false);
        EmojisDaFicha.Guarda("ma_autoria", "2", animado: false);

        Assert.Equal("<:ma_estudio:1> Estúdio", Denia.MalCampoAutoria(TipoDeObra.Anime));
        Assert.Equal("<:ma_autoria:2> Autoria", Denia.MalCampoAutoria(TipoDeObra.Manga));
    }

    [Fact]
    public void O_mapa_conta_quantos_tem()
    {
        EmojisDaFicha.Esquece();

        Assert.Equal(0, EmojisDaFicha.Quantos);

        EmojisDaFicha.Guarda("ma_nota", "1", animado: false);
        EmojisDaFicha.Guarda("ma_elenco", "2", animado: false);

        Assert.Equal(2, EmojisDaFicha.Quantos);
    }
}
