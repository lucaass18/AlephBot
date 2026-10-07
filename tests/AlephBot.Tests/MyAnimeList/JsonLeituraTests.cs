using System.Text.Json;

using AlephBot.Threnodian.MyAnimeList;

namespace AlephBot.Tests.MyAnimeList;

/// <summary>O MAL deixa de fora o que não sabe e o Jikan manda null: nada disso pode virar exceção.</summary>
public class JsonLeituraTests
{
    private static readonly JsonElement Json = JsonDocument.Parse(
        """
        {
          "texto": "oi",
          "nulo": null,
          "numero": 12,
          "zero": 0,
          "negativo": -3,
          "decimal": 8.75,
          "lista": ["a", "", "  ", "b", 3],
          "objetos": [{ "name": "Ação" }, { "name": null }, { "nome": "x" }, { "name": "Drama" }]
        }
        """).RootElement;

    [Fact]
    public void Campo_ausente_ou_null_vira_null()
    {
        Assert.NotNull(Json.Campo("texto"));
        Assert.Null(Json.Campo("não existe"));
        Assert.Null(Json.Campo("nulo"));
    }

    [Fact]
    public void Campo_de_algo_que_não_é_objeto_vira_null()
    {
        Assert.Null(Json.Campo("lista")!.Value.Campo("qualquer"));
    }

    [Fact]
    public void Texto_só_aceita_string()
    {
        Assert.Equal("oi", Json.Texto("texto"));
        Assert.Null(Json.Texto("numero"));
        Assert.Null(Json.Texto("nulo"));
    }

    [Fact]
    public void Contagem_só_aceita_positivo()
    {
        Assert.Equal(12, Json.Contagem("numero"));
        Assert.Null(Json.Contagem("zero"));
        Assert.Null(Json.Contagem("negativo"));
        Assert.Null(Json.Contagem("texto"));
    }

    [Fact]
    public void Decimal_lê_qualquer_número()
    {
        Assert.Equal(8.75, Json.Decimal("decimal"));
        Assert.Equal(12.0, Json.Decimal("numero"));
        Assert.Null(Json.Decimal("texto"));
    }

    [Fact]
    public void Lista_que_não_é_lista_vira_vazia()
    {
        Assert.Empty(Json.Lista("texto"));
        Assert.Equal(5, Json.Lista("lista").Count());
    }

    [Fact]
    public void Textos_pula_vazio_e_o_que_não_é_string()
    {
        Assert.Equal(new[] { "a", "b" }, Json.Textos("lista"));
    }

    [Fact]
    public void Nomes_pega_um_campo_de_cada_objeto()
    {
        Assert.Equal(new[] { "Ação", "Drama" }, Json.Nomes("objetos"));
        Assert.Equal(new[] { "x" }, Json.Nomes("objetos", campo: "nome"));
    }
}
