using Microsoft.AspNetCore.Builder;

namespace AlephBot.Threnodian.Api;

// A camada por cima da chave, pra quem vem de fora: ninguém embute esta API em outro site.
public static class ProteçãoDeFora
{
    // Entra antes do limite de pedidos, pra valer até no 429.
    public static void UseCabeçalhos(WebApplication app) =>
        app.Use((http, next) =>
        {
            var headers = http.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = "frame-ancestors 'none'";

            // o caminho conta qual rota alguém olhava, e isso não é da conta do site de destino
            headers["Referrer-Policy"] = "no-referrer";

            return next(http);
        });
}
