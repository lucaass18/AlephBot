using System.Collections.Immutable;
using System.Text.Json;

using AlephBot.Core.Commands.Music;
using AlephBot.Core.Personality;

using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;

namespace AlephBot.Tests.Commands;

// Caixa branca da formatação das faixas: só dados da faixa, nenhum player nem Lavalink.
public class FormataçãoDaFaixaTests
{
    private static LavalinkTrack Faixa(
        string? título = "Tema", string? autor = "Autor", Uri? endereço = null, bool aoVivo = false,
        string? canalDoArtista = null) => new()
    {
        Title = título!,
        Author = autor!,
        Identifier = "abc",
        SourceName = "http",
        Duration = TimeSpan.FromSeconds(221),
        IsLiveStream = aoVivo,
        Uri = endereço,
        AdditionalInformation = canalDoArtista is null
            ? ImmutableDictionary<string, JsonElement>.Empty
            : ImmutableDictionary<string, JsonElement>.Empty.Add("artistUrl", JsonSerializer.SerializeToElement(canalDoArtista)),
    };

    // Link: faixa.Uri is { } → título clicável
    [Fact]
    public void Faixa_com_endereço_vira_link()
    {
        Assert.Equal("[Tema](https://exemplo.com/tema)", Music.Link(Faixa(endereço: new Uri("https://exemplo.com/tema"))));
    }

    // Link: sem Uri → só o título em negrito
    [Fact]
    public void Faixa_sem_endereço_fica_em_negrito()
    {
        Assert.Equal("**Tema**", Music.Link(Faixa()));
    }

    // Escapa dentro do Link: colchete no título não pode quebrar o link
    [Fact]
    public void Título_com_colchete_é_escapado_no_link()
    {
        Assert.Equal(@"[Tema \[ao vivo\]](https://exemplo.com/)",
            Music.Link(Faixa(título: "Tema [ao vivo]", endereço: new Uri("https://exemplo.com/"))));
    }

    // Artista: CanalDoArtista acha o artistUrl do pluginInfo → nome clicável
    [Fact]
    public void Artista_com_canal_vira_link()
    {
        Assert.Equal("[Autor](https://artista.com)", Music.Artista(Faixa(canalDoArtista: "https://artista.com")));
    }

    // Artista: sem artistUrl, ou com ele vazio → só o nome
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Artista_sem_canal_fica_só_o_nome(string? canal)
    {
        Assert.Equal("Autor", Music.Artista(Faixa(canalDoArtista: canal)));
    }

    // Escapa: sem autor vira "?"
    [Fact]
    public void Artista_sem_nome_vira_interrogação()
    {
        Assert.Equal("?", Music.Artista(Faixa(autor: "")));
    }

    // DuraçãoDe: os dois lados do IsLiveStream
    [Fact]
    public void Transmissão_ao_vivo_não_mostra_duração()
    {
        Assert.Equal(Denia.MúsicaAoVivo, Music.DuraçãoDe(Faixa(aoVivo: true)));
    }

    [Fact]
    public void Faixa_gravada_mostra_a_duração()
    {
        Assert.Equal("3:41", Music.DuraçãoDe(Faixa()));
    }
}

public class QuemPediuTests
{
    private static readonly LavalinkTrack Faixa = new()
    {
        Title = "Tema",
        Author = "Autor",
        Identifier = "abc",
        SourceName = "http",
    };

    // item é FaixaPedida → devolve o nome guardado
    [Fact]
    public void Faixa_pedida_guarda_quem_pediu()
    {
        Assert.Equal("fulano", Music.QuemPediu(new FaixaPedida(new TrackReference(Faixa), "fulano")));
    }

    // item de outro tipo, ou nenhum → null
    [Fact]
    public void Item_comum_ou_ausente_devolve_null()
    {
        Assert.Null(Music.QuemPediu(new TrackQueueItem(new TrackReference(Faixa))));
        Assert.Null(Music.QuemPediu(null));
    }
}

public class AçãoDaPlaylistTests
{
    // as duas comparações do ||, sem diferença de maiúscula
    [Theory]
    [InlineData("add")]
    [InlineData("ADD")]
    [InlineData("adicionar")]
    [InlineData("Adicionar")]
    public void Add_e_adicionar_são_a_mesma_ação(string ação)
    {
        Assert.True(Playlist.ÉAdd(ação));
    }

    [Theory]
    [InlineData("salvar")]
    [InlineData("adiciona")]
    [InlineData("")]
    public void Outras_palavras_não_são_add(string ação)
    {
        Assert.False(Playlist.ÉAdd(ação));
    }
}
