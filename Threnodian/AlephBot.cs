using System.Reflection;

using System.Runtime.InteropServices;

using AlephBot.Config;
using AlephBot.Core.Commands;
using AlephBot.Core.Personality;
using AlephBot.Threnodian.Activity;
using AlephBot.Threnodian.Diagnostics;
using AlephBot.Threnodian.Handlers;

using Lavalink4NET.InactivityTracking;
using Lavalink4NET.InactivityTracking.Extensions;
using Lavalink4NET.InactivityTracking.Trackers.Idle;
using Lavalink4NET.InactivityTracking.Trackers.Users;
using Lavalink4NET.NetCord;

// AddLavalink existe nos dois namespaces: o do Lavalink4NET.NetCord registra o wrapper do
// gateway e chama o do core; o do core, sozinho, deixaria o Lavalink sem saber falar com o
// Discord. Importar os dois deixaria a chamada ambígua, então o core entra por apelido.
using LavalinkCore = Lavalink4NET.Extensions.ServiceCollectionExtensions;

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

    public async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var modo = _config.IsDevelopment ? $"dev (guild {_config.DevGuildId})" : "produção";

        Banner.Print(
            Console.Out,
            _config.Prefix,
            modo,
            ModulesAssembly.GetName().Version?.ToString(3) ?? "dev",
            RuntimeInformation.FrameworkDescription);

        var host = BuildHost(args);

        var logger = host.Services.GetRequiredService<ILogger<AlephBot>>();
        logger.LogInformation(
            "AlephBot iniciando | prefixo: {Prefix} | modo: {Mode}",
            _config.Prefix,
            modo);

        await host.RunAsync(cancellationToken);
    }

    private IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        ConfigureLogging(builder);

        builder.Services.AddSingleton(_config);
        builder.Services.AddSingleton<CommandRegistry>();
        builder.Services.AddSingleton<CommandFailureHandler>();

        builder.Services.AddHostedService<BotActivityService>();
        builder.Services.AddHostedService<CommandAuditService>();

        ConfigureMusic(builder.Services);

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
                options.Prefix = _config.Prefix;
                options.IgnoreCase = true;

                // sem isso as falhas voltam em inglês, direto da NetCord
                options.ResultHandler = provider.GetRequiredService<CommandFailureHandler>();
            });

        var host = builder.Build();

        // registra os módulos ([SlashCommand], [Command], [ComponentInteraction]) do assembly
        host.AddModules(ModulesAssembly);

        return host;
    }

    /// <summary>
    /// O áudio não roda dentro do bot: quem decodifica e manda os pacotes pro Discord é um
    /// servidor Lavalink separado, e o AddLavalink daqui só liga o gateway a ele. Bot de pé
    /// com o Lavalink fora continua respondendo /ping e /ban — só a música fica indisponível,
    /// que é o comportamento que se quer de um serviço externo.
    /// </summary>
    private void ConfigureMusic(IServiceCollection services)
    {
        services.AddLavalink();

        LavalinkCore.ConfigureLavalink(services, options =>
        {
            options.BaseAddress = _config.LavalinkUri;
            options.Passphrase = _config.LavalinkPassword;
            options.Label = "aleph";

            // o container do Lavalink demora mais que o do bot pra aceitar conexão;
            // este é o tempo que eu espero antes de desistir da primeira tentativa
            options.ReadyTimeout = TimeSpan.FromSeconds(20);
        });

        // sozinha num canal eu ficaria conectada pra sempre, gastando voz do servidor:
        // os dois rastreadores cobrem os dois jeitos de isso acontecer — todo mundo saiu
        // (Users) e ninguém mandou tocar mais nada (Idle)
        services
            .AddInactivityTracking()
            .ConfigureInactivityTracking(options =>
            {
                options.DefaultTimeout = _config.MusicIdleTimeout;
                options.TrackingMode = InactivityTrackingMode.Any;

                // eu quero sair do canal, não pausar e ficar lá parada
                options.InactivityBehavior = PlayerInactivityBehavior.None;

                // os padrões da biblioteca só entram quando ninguém registra rastreador
                // nenhum — eles não se somam aos meus. Desligo mesmo assim pra que a lista
                // abaixo seja a única resposta: se alguém apagar uma linha de lá, o
                // rastreamento fica de fora, em vez de voltar calado pro padrão de outra versão
                options.UseDefaultTrackers = false;
            })
            .AddInactivityTracker<UsersInactivityTracker>()
            .AddInactivityTracker<IdleInactivityTracker>();
    }

    private void ConfigureLogging(HostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(_config.LogLevel);
        builder.Logging.AddNLog();
    }
}
