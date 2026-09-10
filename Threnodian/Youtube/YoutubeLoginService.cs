using System.Net.Http.Json;
using System.Text.Json.Serialization;

using AlephBot.Config;
using AlephBot.Core.Personality;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Youtube;

/// <summary>
/// O login do YouTube pedido aqui, no meu console, em vez do log do Lavalink.
///
/// De IP de datacenter o YouTube responde "This video requires login" em vídeo público: a
/// busca passa e o áudio não. Quem usa o token é o Lavalink, mas o fluxo de dispositivo é
/// só HTTP contra o Google — então eu peço o código, espero você autorizar e mostro o
/// refresh token pronto pra colar no .env.
///
/// O token vale para um client específico: estas constantes são as do youtube-source, e
/// mudar qualquer uma delas produz um token que o Lavalink não consegue renovar.
/// </summary>
public sealed class YoutubeLoginService : BackgroundService
{
    private const string ClientId = "861556708454-d6dlm3lh05idd8npek18k6be8ba3oc68.apps.googleusercontent.com";
    private const string ClientSecret = "SboVhoG9s0rNafixCSGGKXAT";
    private const string Escopos = "http://gdata.youtube.com https://www.googleapis.com/auth/youtube";

    private const string UrlCódigo = "https://www.youtube.com/o/oauth2/device/code";
    private const string UrlToken = "https://www.youtube.com/o/oauth2/token";

    // o fluxo de dispositivo antigo do YouTube, que é o que o plugin fala
    private const string TipoDeConcessão = "http://oauth.net/grant_type/device/1.0";
    private const string ModeloDoAparelho = "ytlr::";

    // o Google manda o intervalo na resposta; isto é só o piso de quando ele vem zerado
    private static readonly TimeSpan IntervaloMínimo = TimeSpan.FromSeconds(5);

    private readonly AlephConfig _config;
    private readonly ILogger<YoutubeLoginService> _logger;

    public YoutubeLoginService(AlephConfig config, ILogger<YoutubeLoginService> logger)
    {
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // token guardado é login já feito: o refresh token não vence quando o bot desliga,
        // e pedir outro a cada boot só rende código que ninguém vai digitar. Quem quiser
        // trocar de conta apaga o YOUTUBE_REFRESH_TOKEN e me sobe de novo
        if (_config.YoutubeRefreshToken is not null)
        {
            _logger.LogInformation(
                "Login do YouTube já feito, token veio no ambiente. Apague o YOUTUBE_REFRESH_TOKEN se quiser refazer.");
            return;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        try
        {
            var pedido = await PedirCódigoAsync(http, stoppingToken);

            if (pedido is null)
                return;

            Convite(pedido);

            var token = await EsperarAutorizaçãoAsync(http, pedido, stoppingToken);

            if (token is not null)
                Pronto(token);
        }
        catch (OperationCanceledException)
        {
            // o bot desligou no meio da espera; não é erro
        }
        catch (Exception erro)
        {
            _logger.LogError(erro, "Falha no login do YouTube");
            Recado(Denia.YoutubeFalhou(erro.Message));
        }
    }

    private async Task<CódigoDeDispositivo?> PedirCódigoAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var corpo = new
        {
            client_id = ClientId,
            scope = Escopos,
            device_id = Guid.NewGuid().ToString("N"),
            device_model = ModeloDoAparelho,
        };

        var resposta = await http.PostAsJsonAsync(UrlCódigo, corpo, cancellationToken);
        var código = await resposta.Content.ReadFromJsonAsync<CódigoDeDispositivo>(cancellationToken);

        if (!resposta.IsSuccessStatusCode || código?.UserCode is null)
        {
            _logger.LogError("O Google recusou o pedido de código: {Status}", resposta.StatusCode);
            Recado(Denia.YoutubeFalhou($"o Google respondeu {(int)resposta.StatusCode}"));
            return null;
        }

        return código;
    }

    /// <summary>
    /// Fica perguntando ao Google se você já autorizou. Enquanto ninguém digita o código a
    /// resposta é <c>authorization_pending</c>, que não é erro — é só "ainda não".
    /// </summary>
    private async Task<string?> EsperarAutorizaçãoAsync(
        HttpClient http,
        CódigoDeDispositivo pedido,
        CancellationToken cancellationToken)
    {
        var intervalo = pedido.Interval > 0
            ? TimeSpan.FromSeconds(pedido.Interval)
            : IntervaloMínimo;

        var corpo = new
        {
            client_id = ClientId,
            client_secret = ClientSecret,
            code = pedido.DeviceCode,
            grant_type = TipoDeConcessão,
        };

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(intervalo, cancellationToken);

            var resposta = await http.PostAsJsonAsync(UrlToken, corpo, cancellationToken);
            var token = await resposta.Content.ReadFromJsonAsync<RespostaDeToken>(cancellationToken);

            if (token?.RefreshToken is { Length: > 0 } refresh)
                return refresh;

            switch (token?.Error)
            {
                case "authorization_pending":
                    continue;

                // pedi rápido demais; o Google pede pra afrouxar e eu afrouxo
                case "slow_down":
                    intervalo += IntervaloMínimo;
                    continue;

                case "access_denied":
                    Recado(Denia.YoutubeNegado());
                    return null;

                case "expired_token":
                    Recado(Denia.YoutubeExpirou());
                    return null;

                default:
                    _logger.LogError("Login do YouTube recusado: {Erro}", token?.Error ?? "sem motivo");
                    Recado(Denia.YoutubeFalhou(token?.Error ?? "motivo desconhecido"));
                    return null;
            }
        }

        return null;
    }

    // ---- console -------------------------------------------------------------
    //
    // sai direto no Console, como o banner e a despedida: com timestamp e nome de logger
    // na frente de cada linha, um código de seis dígitos vira caça ao tesouro

    private static void Convite(CódigoDeDispositivo pedido)
    {
        var (roxo, cinza, fim) = Paleta();

        Console.WriteLine();
        Console.WriteLine($"  {roxo}Denia{fim} {cinza}· {Denia.YoutubeTítulo}{fim}");
        Console.WriteLine($"  {cinza}{Denia.YoutubeAbertura()}{fim}");
        Console.WriteLine();
        Console.WriteLine($"  {cinza}1.{fim} abre {roxo}{pedido.VerificationUrl}{fim}");
        Console.WriteLine($"  {cinza}2.{fim} entra com uma conta descartável {cinza}(não a sua principal){fim}");
        Console.WriteLine($"  {cinza}3.{fim} digita o código {roxo}{pedido.UserCode}{fim}");
        Console.WriteLine();
        Console.WriteLine($"  {cinza}{Denia.YoutubeEsperando()}{fim}");
        Console.WriteLine();
    }

    private static void Pronto(string refreshToken)
    {
        var (roxo, cinza, fim) = Paleta();

        Console.WriteLine();
        Console.WriteLine($"  {roxo}Denia{fim} {cinza}· {Denia.YoutubeTítulo}{fim}");
        Console.WriteLine($"  {cinza}{Denia.YoutubePronto()}{fim}");
        Console.WriteLine();
        Console.WriteLine($"  {roxo}YOUTUBE_REFRESH_TOKEN={refreshToken}{fim}");
        Console.WriteLine();
        Console.WriteLine($"  {cinza}{Denia.YoutubeOndeColar()}{fim}");
        Console.WriteLine();
    }

    private static void Recado(string fala)
    {
        var (roxo, cinza, fim) = Paleta();

        Console.WriteLine();
        Console.WriteLine($"  {roxo}Denia{fim} {cinza}· {Denia.YoutubeTítulo}{fim}");
        Console.WriteLine($"  {cinza}{fala}{fim}");
        Console.WriteLine();
    }

    private static (string Roxo, string Cinza, string Fim) Paleta()
    {
        var cor = Banner.UsaCor();
        return (cor ? Banner.Roxo : "", cor ? Banner.Cinza : "", cor ? Banner.Fim : "");
    }

    // ---- respostas do Google -------------------------------------------------

    private sealed record CódigoDeDispositivo(
        [property: JsonPropertyName("device_code")] string DeviceCode,
        [property: JsonPropertyName("user_code")] string UserCode,
        [property: JsonPropertyName("verification_url")] string VerificationUrl,
        [property: JsonPropertyName("interval")] int Interval);

    private sealed record RespostaDeToken(
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("error")] string? Error);
}
