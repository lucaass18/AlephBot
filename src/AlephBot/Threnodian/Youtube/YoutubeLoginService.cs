using System.Net.Http.Json;
using System.Text.Json.Serialization;

using AlephBot.Config;
using AlephBot.Core.Personality;

using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Events.Players;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Youtube;

// O login do YouTube: código no console, token guardado, entregue ao Lavalink a cada conexão.
// Mudar as constantes do youtube-source gera um token que o Lavalink não renova.
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

    // no desligamento o Lavalink pode já estar caindo junto; não seguro o bot por causa disso
    private static readonly TimeSpan EsperaNoDesligamento = TimeSpan.FromSeconds(3);

    // uma fila inteira falhando dispara um aviso por faixa; conferir o token uma vez basta
    private static readonly TimeSpan IntervaloEntreConferências = TimeSpan.FromMinutes(5);

    private readonly AlephConfig _config;
    private readonly YoutubeTokenStore _tokens;
    private readonly LavalinkYoutube _lavalink;
    private readonly IAudioService _áudio;
    private readonly ILogger<YoutubeLoginService> _logger;

    // uma entrega (ou um login) por vez: o Lavalink pode reconectar no meio de uma
    private readonly SemaphoreSlim _umaPorVez = new(1, 1);

    // o Lavalink tem o token nas mãos, mas o Google recusou: "já está lá" não vale como pronto
    private bool _recusado;

    private DateTimeOffset _últimaConferência = DateTimeOffset.MinValue;

    private CancellationToken _parando;

    public YoutubeLoginService(
        AlephConfig config,
        YoutubeTokenStore tokens,
        LavalinkYoutube lavalink,
        IAudioService áudio,
        ILogger<YoutubeLoginService> logger)
    {
        _config = config;
        _tokens = tokens;
        _lavalink = lavalink;
        _áudio = áudio;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _parando = stoppingToken;
        _tokens.Carregar();

        // em casa: sem token e sem pedido de login, o YouTube toca sem conta nenhuma
        if (_tokens.Token is null && !_config.YoutubeLogin)
            return;

        // toda vez que o Lavalink (re)aparece ele acorda sem token, porque o yml não traz nenhum
        _áudio.ConnectionReady += AoConectarAsync;

        // e se o token morrer com tudo de pé, quem me conta é a faixa que falha pedindo login
        _áudio.TrackException += AoFalharFaixaAsync;

        try
        {
            // a primeira conexão pode ter vindo antes de eu me inscrever; garanto a entrega
            await _áudio.WaitForReadyAsync(stoppingToken);
            await EntregarAsync(conferir: false, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // o bot desligou antes do Lavalink aparecer; não é erro
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _áudio.ConnectionReady -= AoConectarAsync;
        _áudio.TrackException -= AoFalharFaixaAsync;

        await base.StopAsync(cancellationToken);

        // o Google pode ter trocado o token na renovação; o novo só existe na memória do Lavalink
        if (_tokens.Token is null)
            return;

        using var curto = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        curto.CancelAfter(EsperaNoDesligamento);

        var atual = await _lavalink.TokenAtualAsync(curto.Token);

        if (atual is not null && atual != _tokens.Token)
        {
            _tokens.Guardar(atual);
            _logger.LogInformation("O Google trocou o refresh token do YouTube; guardei o novo");
        }
    }

    private Task AoConectarAsync(object sender, ConnectionReadyEventArgs eventArgs)
    {
        // não seguro o loop do node: se o token morreu, a entrega espera alguém digitar um código
        _ = Task.Run(() => EntregarAsync(conferir: false, _parando), _parando);

        return Task.CompletedTask;
    }

    // Faixa que morre pedindo login diz que meu token não vale mais; confiro na hora.
    private Task AoFalharFaixaAsync(object sender, TrackExceptionEventArgs eventArgs)
    {
        if (_tokens.Token is null || !PedeLogin(eventArgs.Exception.Message))
            return Task.CompletedTask;

        var agora = DateTimeOffset.UtcNow;

        if (agora - _últimaConferência < IntervaloEntreConferências)
            return Task.CompletedTask;

        _últimaConferência = agora;
        _ = Task.Run(() => EntregarAsync(conferir: true, _parando), _parando);

        return Task.CompletedTask;
    }

    // "This video requires login" e "Sign in to confirm you're not a bot".
    private static bool PedeLogin(string? motivo) =>
        motivo is not null
        && (motivo.Contains("login", StringComparison.OrdinalIgnoreCase)
            || motivo.Contains("sign in", StringComparison.OrdinalIgnoreCase));

    // Põe o token no Lavalink se ainda não está lá. Com conferir, o Google diz se ele vale.
    private async Task EntregarAsync(bool conferir, CancellationToken cancellationToken)
    {
        // uma em andamento já cuida disso — inclusive se ela está esperando o código
        if (!await _umaPorVez.WaitAsync(0, cancellationToken))
            return;

        try
        {
            if (_tokens.Token is null)
            {
                // primeira vez: YOUTUBE_LOGIN ligado e nenhum token guardado
                await LoginAsync(cancellationToken);
                return;
            }

            // entregar de novo o que ele já tem só faria o Lavalink renovar o acesso à toa
            if (!conferir && !_recusado && await _lavalink.TokenAtualAsync(cancellationToken) == _tokens.Token)
                return;

            var entrega = await _lavalink.EntregarAsync(_tokens.Token, cancellationToken);

            switch (entrega.Estado)
            {
                case LavalinkYoutube.Entrega.Resultado.Aceito:
                    _recusado = false;
                    _logger.LogInformation("Token do YouTube entregue ao Lavalink");
                    break;

                case LavalinkYoutube.Entrega.Resultado.Recusado when entrega.TokenMorreu:
                    _recusado = true;
                    _logger.LogWarning("O Google invalidou o refresh token do YouTube: {Motivo}", entrega.Motivo);
                    Recado(Denia.YoutubeTokenMorreu());
                    await LoginAsync(cancellationToken);
                    break;

                case LavalinkYoutube.Entrega.Resultado.Recusado:
                    _recusado = true;
                    _logger.LogError("O Lavalink não aceitou o token do YouTube: {Motivo}", entrega.Motivo);
                    break;

                default:
                    _logger.LogDebug("Lavalink fora do ar; entrego o token do YouTube quando ele voltar");
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // desligando; o token guardado sobrevive pro próximo boot
        }
        catch (Exception erro)
        {
            _logger.LogError(erro, "Falha cuidando do token do YouTube");
            Recado(Denia.YoutubeFalhou(erro.Message));
        }
        finally
        {
            _umaPorVez.Release();
        }
    }

    // O fluxo de dispositivo inteiro: código no console, espera, guarda e entrega.
    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        var pedido = await PedirCódigoAsync(http, cancellationToken);

        if (pedido is null)
            return;

        Convite(pedido);

        var token = await EsperarAutorizaçãoAsync(http, pedido, cancellationToken);

        if (token is null)
            return;

        // guardo antes de entregar: se o Lavalink estiver fora agora, a próxima conexão entrega
        _tokens.Guardar(token);

        var entrega = await _lavalink.EntregarAsync(token, cancellationToken);
        _recusado = entrega.Estado == LavalinkYoutube.Entrega.Resultado.Recusado;

        if (_recusado)
            _logger.LogError("O Lavalink não aceitou o token recém-criado: {Motivo}", entrega.Motivo);

        Pronto();
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

    // Fica perguntando se você já autorizou; authorization_pending não é erro, é "ainda não".
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

    // sai direto no Console, como o banner: timestamp na frente faz do código uma caça ao tesouro

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

    // o token não sai mais aqui: está no arquivo, e log é lugar de leitura, não de segredo
    private void Pronto()
    {
        var (roxo, cinza, fim) = Paleta();

        Console.WriteLine();
        Console.WriteLine($"  {roxo}Denia{fim} {cinza}· {Denia.YoutubeTítulo}{fim}");
        Console.WriteLine($"  {cinza}{Denia.YoutubePronto()}{fim}");
        Console.WriteLine($"  {cinza}{Denia.YoutubeOndeEstá(_tokens.Caminho)}{fim}");
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
