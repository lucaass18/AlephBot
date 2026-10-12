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

// existe AddLavalink nos dois namespaces, e importar os dois deixa a chamada ambígua.
// o do NetCord é o que serve; o core entra por apelido só pra eu alcançar o Configure
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


namespace AlephBot.Threnodian;

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

    /// <summary>A versão do csproj, como sai no banner e na API.</summary>
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

    /// <summary>
    /// Com API_KEY o host vira um app web: o mesmo bot, os mesmos serviços, com um Kestrel a
    /// mais dentro. Sem ela fica o host genérico de sempre — nenhuma porta aberta à toa.
    /// </summary>
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
                // o prefixo pode ser outro em cada servidor (/config), com o do .env atrás.
                // Sem isto o /config guardava a escolha e nada a lia: o bot seguia ouvindo
                // só o prefixo do arquivo, e quem trocou ficava sem comando de texto
                options.GetPrefixLengthAsync = async (mensagem, _, serviços) =>
                {
                    // o caminho pronto do NetCord (options.Prefix) ignora mensagem de bot
                    // sozinho; assumindo o lugar dele, a conta é minha — sem isto o bot
                    // obedeceria outro bot, e a si mesmo, que é como nasce um laço
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

        // depois do gateway de propósito: os serviços param na ordem inversa, e este precisa
        // tirar a última foto dos players — com a posição exata — enquanto tudo ainda está de pé
        builder.Services.AddHostedService<PlayerResumeService>();
    }

    /// <summary>
    /// O que devolver quando a mensagem não é pra mim. Qualquer negativo serve: o NetCord
    /// testa <c>&lt; 0</c> antes de cortar o prefixo fora do texto.
    /// </summary>
    internal const int NãoÉComando = -1;

    /// <summary>
    /// Quantas letras o prefixo ocupa nesta mensagem, ou <see cref="NãoÉComando"/> quando
    /// ela não é. A comparação ignora maiúscula: com prefixo de uma letra tanto faz, mas
    /// quem puser "aleph" no /config não vai querer explicar a diferença pro servidor.
    /// </summary>
    internal static int TamanhoDoPrefixo(string conteúdo, string prefixo) =>
        conteúdo.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)
            ? prefixo.Length
            : NãoÉComando;

    /// <summary>
    /// O banco, se houver um. Sem DATABASE_URL eu subo sem ele e cada servidor fica com o
    /// padrão do .env — que é exatamente como era antes de existir banco aqui. Nada do que
    /// faz o bot responder passa por este caminho; o Postgres guarda escolha, não comando.
    /// </summary>
    private void ConfigureBanco(IServiceCollection services)
    {
        if (!string.IsNullOrWhiteSpace(_config.DatabaseUrl))
        {
            // fábrica, e não DbContext direto: quem lê a configuração é singleton (o prefixo
            // é perguntado a cada mensagem), e singleton não pode depender de scoped
            services.AddDbContextFactory<AlephDb>(options => options.UseNpgsql(
                _config.DatabaseUrl,
                npgsql => npgsql.MigrationsHistoryTable("__migracoes", AlephDb.Schema)));

            services.AddSingleton<MigracaoDoBanco>();
            services.AddHostedService(provider => provider.GetRequiredService<MigracaoDoBanco>());

            // o que ficou na tabela do n8n entra antes de tudo, pra linha do tempo não
            // nascer com um buraco entre o histórico velho e a primeira coleta nova
            services.AddHostedService<ImportadorDoHistorico>();

            // quem sabe os números é o BotStatus, que só existe junto da API: sem API_KEY
            // não há o que coletar, e o histórico simplesmente não começa
            if (_config.Api is not null)
                services.AddHostedService<ColetorDeStats>();
        }

        services.AddSingleton<PlaylistsSalvas>(provider => new PlaylistsSalvas(
            provider.GetService<IDbContextFactory<AlephDb>>(),
            provider.GetService<MigracaoDoBanco>(),
            provider.GetRequiredService<ILogger<PlaylistsSalvas>>()));

        services.AddSingleton<ConfigDosServidores>(provider => new ConfigDosServidores(
            provider.GetService<IDbContextFactory<AlephDb>>(),
            provider.GetService<MigracaoDoBanco>(),
            _config,
            provider.GetRequiredService<ILogger<ConfigDosServidores>>()));
    }

    /// <summary>
    /// O áudio não roda dentro de mim: quem decodifica é um Lavalink separado, e aqui eu só
    /// me ligo nele. Com ele fora eu sigo respondendo /ping e /ban — só a música é que falta.
    /// </summary>
    private void ConfigureMusic(IServiceCollection services)
    {
        services.AddLavalink();

        // o /playlist add procura playlist pelo nome, coisa que o Lavalink não sabe fazer
        services.AddSingleton<YoutubeSearch>();

        // o login do YouTube: o token fica comigo, e sou eu quem entrega ele ao Lavalink a
        // cada conexão. Registrado sempre — em casa, sem token e sem YOUTUBE_LOGIN, ele
        // vê que não tem o que fazer e sai
        services.AddSingleton<YoutubeTokenStore>();
        services.AddSingleton<LavalinkYoutube>();
        services.AddHostedService<YoutubeLoginService>();

        // o áudio do YouTube pelo yt-audio do compose: o player pega daqui na hora de pôr
        // faixa na fila. Sem YOUTUBE_AUDIO_URI ele não envolve nada e tudo vai pelo plugin
        services.AddSingleton<YoutubeAudio>();

        // a foto de cada player, pra eu voltar ao mesmo ponto depois de um restart. quem tira
        // é o player; quem revela é o PlayerResumeService, registrado lá no fim do BuildHost
        services.AddSingleton<PlayerSnapshotStore>();

        LavalinkCore.ConfigureLavalink(services, options =>
        {
            options.BaseAddress = _config.LavalinkUri;
            options.Passphrase = _config.LavalinkPassword;
            options.Label = "aleph";

            // o container do Lavalink sobe mais devagar que o meu; espero por ele
            options.ReadyTimeout = TimeSpan.FromSeconds(20);
        });

        // sozinha num canal eu ficaria ali pra sempre. os dois rastreadores cobrem os dois
        // jeitos de isso acontecer: todo mundo saiu (Users) e ninguém pediu mais nada (Idle)
        services
            .AddInactivityTracking()
            .ConfigureInactivityTracking(options =>
            {
                options.DefaultTimeout = _config.MusicIdleTimeout;
                options.TrackingMode = InactivityTrackingMode.Any;

                // eu quero sair do canal, não pausar e ficar lá parada
                options.InactivityBehavior = PlayerInactivityBehavior.None;

                // desligo pra lista abaixo ser a única resposta: apagou uma linha de lá, o
                // rastreamento sai — em vez de voltar calado pro padrão de outra versão
                options.UseDefaultTrackers = false;
            })
            .AddInactivityTracker<UsersInactivityTracker>()
            .AddInactivityTracker<IdleInactivityTracker>();
    }

    /// <summary>
    /// O /ma: quem procura no MyAnimeList e quem traduz a sinopse. As fontes são montadas em
    /// fila — com Client ID no .env a API oficial atende primeiro, e o AniList fica de
    /// reserva pra quando ela cair ou não conhecer a obra. Sem chave, só o AniList, que não
    /// pede nada. A fila existe porque antes era uma fonte só, e o /ma caiu junto com ela.
    /// </summary>
    private void ConfigureAnime(IServiceCollection services)
    {
        services.AddSingleton<GoogleTranslator>();

        services.AddSingleton<IMyAnimeList>(provider =>
        {
            List<IMyAnimeList> fontes = _config.MalClientId is { } clientId
                ? [new MalApi(clientId), new AniList()]
                : [new AniList()];

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
