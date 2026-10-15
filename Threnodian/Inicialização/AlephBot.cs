using System.Reflection;

using System.Runtime.InteropServices;

using AlephBot.Config;
using AlephBot.Core.Commands;
using AlephBot.Core.Personality;
using AlephBot.Threnodian.Activity;
using AlephBot.Threnodian.Api;
using AlephBot.Threnodian.Dados;
using AlephBot.Threnodian.Diagnostics;
using AlephBot.Threnodian.Handlers;
using AlephBot.Threnodian.MyAnimeList;
using AlephBot.Threnodian.Players;
using AlephBot.Threnodian.Translation;
using AlephBot.Threnodian.Youtube;

using Lavalink4NET.InactivityTracking;
using Lavalink4NET.InactivityTracking.Extensions;
using Lavalink4NET.InactivityTracking.Trackers.Idle;
using Lavalink4NET.InactivityTracking.Trackers.Users;
using Lavalink4NET.NetCord;

// AddLavalink existe nos dois namespaces; o apelido desfaz a ambiguidade.
using LavalinkCore = Lavalink4NET.Extensions.ServiceCollectionExtensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.Commands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

using NLog.Extensions.Logging;


namespace AlephBot.Threnodian.Inicialização;

public sealed class AlephBot
{
    // assembly onde estão os módulos de comandos e os gateway handlers
    private static readonly Assembly ModulesAssembly = typeof(AlephBot).Assembly;

    private const GatewayIntents Intents =                                                                                                                                                                                           
        GatewayIntents.Guilds                                                                                                                                                                                                        
        | GatewayIntents.GuildUsers                                                                                                                                                                                                  
        | GatewayIntents.GuildMessages                                                                                                                                                                                               
        | GatewayIntents.DirectMessages                                                                                                                                                                                              
        | GatewayIntents.MessageContent      // precisa estar ligado no portal do Discord                                                                                                                                            
        | GatewayIntents.GuildPresences      // idem — privilegiada, necessária pra contar online                                                                                                                                    
        | GatewayIntents.GuildVoiceStates;   // necessário pro Lavalink4NET    

    private readonly AlephConfig _config;

    public AlephBot(AlephConfig config)
    {
        _config = config;
    }

    // A versão do csproj, como sai no banner e na API.
    public static string Version => ModulesAssembly.GetName().Version?.ToString(3) ?? "dev";

    public async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var modo = _config.IsDevelopment ? $"dev (guild {_config.DevGuildId})" : "produção";

        Banner.Print(
            Console.Out,
            _config.Prefix,
            modo,
            Version,
            RuntimeInformation.FrameworkDescription);

        var host = BuildHost(args);

        var logger = host.Services.GetRequiredService<ILogger<AlephBot>>();
        logger.LogInformation(
            "AlephBot iniciando | prefixo: {Prefix} | modo: {Mode}",
            _config.Prefix,
            modo);

        if (_config.Api is { } api)
            logger.LogInformation("API ligada | {Endereço}", AlephApi.Endereço(api));

        await host.RunAsync(cancellationToken);
    }

    // Com API_KEY o host vira app web, com um Kestrel dentro. Sem ela, nenhuma porta aberta à toa.
    private IHost BuildHost(string[] args)
    {
        IHost host;

        if (_config.Api is { } api)
        {
            var builder = WebApplication.CreateSlimBuilder(args);

            ConfigureServices(builder);
            AlephApi.Configure(builder, api);

            var app = builder.Build();
            AlephApi.Map(app);

            host = app;
        }
        else
        {
            var builder = Host.CreateApplicationBuilder(args);

            ConfigureServices(builder);

            host = builder.Build();
        }

        // registra os módulos ([SlashCommand], [Command], [ComponentInteraction]) do assembly
        host.AddModules(ModulesAssembly);

        return host;
    }

    private void ConfigureServices(IHostApplicationBuilder builder)
    {
        ConfigureLogging(builder);

        builder.Services.AddSingleton(_config);
        builder.Services.AddSingleton<CommandRegistry>();
        builder.Services.AddSingleton<CommandFailureHandler>();

        builder.Services.AddHostedService<BotActivityService>();
        builder.Services.AddHostedService<CommandAuditService>();

        ConfigureBanco(builder.Services);
        ConfigureMusic(builder.Services);
        ConfigureAnime(builder.Services);

        builder.Services
            .AddDiscordGateway(options =>
            {
                options.Token = _config.Token;
                options.Intents = Intents;
                options.Presence = new PresenceProperties(UserStatusType.Online)
                {
                    // Custom exibe o texto do campo State, não do Name — Playing usa o Name
                    Activities = [new UserActivityProperties("iniciando...", UserActivityType.Playing)],
                };
            })
            .AddGatewayHandlers(ModulesAssembly)   // IMessageCreateGatewayHandler, IReadyGatewayHandler, etc.
            .AddApplicationCommands()              // slash / user / message commands
            .AddComponentInteractions()            // botões, selects, modais
            .AddCommands<CommandContext>((options, provider) =>
            {
                // o prefixo pode ser outro em cada servidor (/config), com o do .env atrás
                options.GetPrefixLengthAsync = async (mensagem, _, serviços) =>
                {
                    // assumindo o lugar do NetCord, a conta é minha: sem isto o bot se obedeceria
                    if (mensagem.Author.IsBot)
                        return NãoÉComando;

                    var prefixo = mensagem.GuildId is { } servidor
                        ? await serviços.GetRequiredService<ConfigDosServidores>().PrefixoAsync(servidor)
                        : _config.Prefix;

                    return TamanhoDoPrefixo(mensagem.Content, prefixo);
                };

                options.IgnoreCase = true;

                // sem isso as falhas voltam em inglês, direto da NetCord
                options.ResultHandler = provider.GetRequiredService<CommandFailureHandler>();
            });

        // depois do gateway de propósito: ele tira a última foto dos players com tudo ainda de pé
        builder.Services.AddHostedService<PlayerResumeService>();
    }

    // O que devolver quando a mensagem não é pra mim — o NetCord só testa < 0.
    internal const int NãoÉComando = -1;

    // Quantas letras o prefixo ocupa, ou NãoÉComando. Ignora maiúscula, que ninguém quer explicar.
    internal static int TamanhoDoPrefixo(string conteúdo, string prefixo) =>
        conteúdo.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)
            ? prefixo.Length
            : NãoÉComando;

    // O banco, se houver. Sem DATABASE_URL cada servidor fica com o padrão do .env.
    private void ConfigureBanco(IServiceCollection services)
    {
        if (!string.IsNullOrWhiteSpace(_config.DatabaseUrl))
        {
            // fábrica, e não DbContext direto: quem lê a config é singleton, e não pega scoped
            services.AddDbContextFactory<AlephDb>(options => options.UseNpgsql(
                _config.DatabaseUrl,
                npgsql => npgsql.MigrationsHistoryTable("__migracoes", AlephDb.Schema)));

            services.AddSingleton<MigracaoDoBanco>();
            services.AddHostedService(provider => provider.GetRequiredService<MigracaoDoBanco>());

            // o que ficou na tabela do n8n entra antes, pra linha do tempo não nascer com buraco
            services.AddHostedService<ImportadorDoHistorico>();

            // quem sabe os números é o BotStatus, que só existe junto da API
            if (_config.Api is not null)
                services.AddHostedService<ColetorDeStats>();
        }

        services.AddSingleton<PlaylistsSalvas>(provider => new PlaylistsSalvas(
            provider.GetService<IDbContextFactory<AlephDb>>(),
            provider.GetService<MigracaoDoBanco>(),
            provider.GetRequiredService<ILogger<PlaylistsSalvas>>()));

        // as fichas que o /ma já mostrou; sem banco ela se declara indisponível
        services.AddSingleton<IFichasGuardadas>(provider => new ObrasEmCache(
            provider.GetService<IDbContextFactory<AlephDb>>(),
            provider.GetService<MigracaoDoBanco>(),
            provider.GetRequiredService<ILogger<ObrasEmCache>>()));

        services.AddSingleton<ConfigDosServidores>(provider => new ConfigDosServidores(
            provider.GetService<IDbContextFactory<AlephDb>>(),
            provider.GetService<MigracaoDoBanco>(),
            _config,
            provider.GetRequiredService<ILogger<ConfigDosServidores>>()));
    }

    // O áudio não roda dentro de mim: quem decodifica é o Lavalink, e aqui eu só me ligo nele.
    private void ConfigureMusic(IServiceCollection services)
    {
        services.AddLavalink();

        // o /playlist add procura playlist pelo nome, coisa que o Lavalink não sabe fazer
        services.AddSingleton<YoutubeSearch>();

        // o token fica comigo, e sou eu quem entrega ele ao Lavalink a cada conexão
        services.AddSingleton<YoutubeTokenStore>();
        services.AddSingleton<LavalinkYoutube>();
        services.AddHostedService<YoutubeLoginService>();

        // o áudio do YouTube pelo yt-audio; sem YOUTUBE_AUDIO_URI tudo vai pelo plugin
        services.AddSingleton<YoutubeAudio>();

        // a foto de cada player, pra eu voltar ao mesmo ponto depois de um restart
        services.AddSingleton<PlayerSnapshotStore>();

        LavalinkCore.ConfigureLavalink(services, options =>
        {
            options.BaseAddress = _config.LavalinkUri;
            options.Passphrase = _config.LavalinkPassword;
            options.Label = "aleph";

            // o container do Lavalink sobe mais devagar que o meu; espero por ele
            options.ReadyTimeout = TimeSpan.FromSeconds(20);
        });

        // os dois rastreadores cobrem as duas formas: todos saíram, e ninguém pediu nada
        services
            .AddInactivityTracking()
            .ConfigureInactivityTracking(options =>
            {
                options.DefaultTimeout = _config.MusicIdleTimeout;
                options.TrackingMode = InactivityTrackingMode.Any;

                // eu quero sair do canal, não pausar e ficar lá parada
                options.InactivityBehavior = PlayerInactivityBehavior.None;

                // desligo pra lista abaixo ser a única resposta: apagou uma linha, o rastreio sai
                options.UseDefaultTrackers = false;
            })
            .AddInactivityTracker<UsersInactivityTracker>()
            .AddInactivityTracker<IdleInactivityTracker>();
    }

    // O /ma: as fontes em fila — a API oficial na frente quando tem chave, o AniList de reserva.
    private void ConfigureAnime(IServiceCollection services)
    {
        services.AddSingleton<GoogleTranslator>();

        // o MangaDex é reserva de mangá e quem traz a sinopse em português
        services.AddSingleton<MangaDex>();
        services.AddSingleton<ISinopseEmPortuguês>(provider => provider.GetRequiredService<MangaDex>());

        services.AddSingleton<IMyAnimeList>(provider =>
        {
            List<IMyAnimeList> fontes = _config.MalClientId is { } clientId
                ? [new MalApi(clientId), new AniList(), provider.GetRequiredService<MangaDex>()]
                : [new AniList(), provider.GetRequiredService<MangaDex>()];

            return new FontesEmCadeia(provider.GetRequiredService<ILogger<FontesEmCadeia>>(), fontes);
        });
    }

    private void ConfigureLogging(IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(_config.LogLevel);
        builder.Logging.AddNLog();
    }
}
