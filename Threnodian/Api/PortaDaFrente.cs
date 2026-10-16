using AlephBot.Config;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AlephBot.Threnodian.Api;

// A raiz do endereço: a tela que pergunta por qual porta entrar. Sem senha de dono não há
// escolha nenhuma, e ela leva direto pra documentação.
internal static class PortaDaFrente
{
    private static readonly string Documentação = $"{ApiDocs.Página}/";

    // As duas portas, e o que muda de uma pra outra: é isso que faz alguém escolher.
    private static readonly string Tela = Moldura.De($$"""
          <h1>AlephBot API</h1>
          <p>A documentação é aberta: entra quem quiser. A porta de dono pede senha e traz a
          IA do Scalar na página.</p>
          <a class="botão" href="{{Documentação}}">Entrar como visitante</a>
          <a class="botão vazio" href="{{LoginDosDocs.Entrar}}">Entrar como dono</a>
        """);

    public static void Map(WebApplication app, ApiConfig api) =>
        app.MapGet("/", () => api.DocsAdminPassword is null
                ? Results.Redirect(Documentação)
                : Results.Content(Tela, Moldura.Tipo))
            .ExcludeFromDescription();
}
