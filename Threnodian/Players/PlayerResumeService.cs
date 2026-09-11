using AlephBot.Core.Commands.Music;

using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Tracks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace AlephBot.Threnodian.Players;

/// <summary>
/// A volta depois de um restart: pra cada player que ficou na foto, entro de novo no canal,
/// retomo a faixa de onde parou e refaço a fila. Ninguém precisa digitar /play de novo.
///
/// Só volto onde ainda tem gente: canal vazio é o rastreador de inatividade quem teria
/// esvaziado, e foto velha demais é de uma sessão que já acabou por conta própria.
///
/// Registrado por último de propósito: no desligamento os serviços param na ordem inversa,
/// e eu preciso tirar a última foto — com a posição exata — antes do gateway e dos players
/// irem embora.
/// </summary>
public sealed class PlayerResumeService : BackgroundService
{
    /// <summary>Foto mais velha que isso é de uma sessão que já acabou; não volto pra ela.</summary>
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(30);

    // o cache de guilds enche depois do READY, um GUILD_CREATE de cada vez
    private static readonly TimeSpan EsperaPelaGuild = TimeSpan.FromSeconds(30);

    /// <summary>Abaixo disso a faixa mal começou; voltar do começo é mais limpo que um seek.</summary>
    private static readonly TimeSpan PosiçãoMínima = TimeSpan.FromSeconds(2);

    private readonly IAudioService _áudio;
    private readonly GatewayClient _client;
    private readonly PlayerSnapshotStore _fotos;
    private readonly ILogger<PlayerResumeService> _logger;

    public PlayerResumeService(
        IAudioService áudio,
        GatewayClient client,
        PlayerSnapshotStore fotos,
        ILogger<PlayerResumeService> logger)
    {
        _áudio = áudio;
        _client = client;
        _fotos = fotos;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var fotos = _fotos.Carregar();

        if (fotos.Count == 0)
            return;

        try
        {
            await _áudio.WaitForReadyAsync(stoppingToken);

            foreach (var foto in fotos)
            {
                try
                {
                    await RetomarAsync(foto, stoppingToken);
                }
                catch (Exception erro) when (erro is not OperationCanceledException)
                {
                    _logger.LogError(erro, "Não consegui retomar a música na guild {Guild}", foto.GuildId);
                    _fotos.Esquecer(foto.GuildId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // desliguei antes de voltar; as fotos ficam pro próximo boot
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        // a última foto de cada player, com a posição de agora; depois dela os players são
        // destruídos e tentariam apagar a própria foto — congelado, o arquivo fica como está
        try
        {
            foreach (var player in _áudio.Players.Players.OfType<AlephPlayer>())
                player.Fotografar();
        }
        catch (Exception erro)
        {
            // desligamento não espera por foto: o que já está no arquivo serve
            _logger.LogWarning(erro, "Não consegui fotografar os players antes de desligar");
        }

        _fotos.Congelar();

        return base.StopAsync(cancellationToken);
    }

    private async Task RetomarAsync(PlayerSnapshot foto, CancellationToken cancellationToken)
    {
        if (foto.Atual is null)
        {
            // nada tocava; a fila sem faixa atual é um /stop, e /stop não se retoma
            _fotos.Esquecer(foto.GuildId);
            return;
        }

        if (DateTimeOffset.UtcNow - foto.SalvoEm > Validade)
        {
            _logger.LogInformation("Foto da guild {Guild} é de {Quando}; velha demais pra retomar", foto.GuildId, foto.SalvoEm);
            _fotos.Esquecer(foto.GuildId);
            return;
        }

        // alguém já pediu música antes de eu chegar aqui: o player novo manda, e a foto dele já cobre a minha
        if (_áudio.Players.Players.Any(player => player.GuildId == foto.GuildId))
            return;

        var guild = await EsperarGuildAsync(foto.GuildId, cancellationToken);

        if (guild is null)
        {
            _logger.LogInformation("Guild {Guild} não apareceu no cache; não volto pra lá", foto.GuildId);
            _fotos.Esquecer(foto.GuildId);
            return;
        }

        if (!AlguémNoCanal(guild, foto.CanalDeVoz))
        {
            _logger.LogInformation("Ninguém mais no canal {Canal} de {Guild}; não volto pra tocar sozinha", foto.CanalDeVoz, guild.Name);
            _fotos.Esquecer(foto.GuildId);
            return;
        }

        if (!LavalinkTrack.TryParse(foto.Atual.Dados, null, out var atual))
        {
            _logger.LogWarning("A faixa guardada da guild {Guild} não decodifica mais", guild.Name);
            _fotos.Esquecer(foto.GuildId);
            return;
        }

        var canal = await CanalDeTextoAsync(foto.CanalDeTexto, cancellationToken);
        var resultado = await Music.VoltarAsync(_áudio, foto.GuildId, foto.CanalDeVoz, canal, cancellationToken);

        if (!resultado.IsSuccess)
        {
            _logger.LogWarning("Não consegui voltar pro canal {Canal} de {Guild}: {Status}", foto.CanalDeVoz, guild.Name, resultado.Status);
            _fotos.Esquecer(foto.GuildId);
            return;
        }

        var player = resultado.Player;

        await player.SetVolumeAsync(foto.Volume, cancellationToken);
        player.RepeatMode = foto.Repetição;

        // marcada como anunciada: quem apresenta a volta sou eu, com a posição, não o "tocando agora"
        var item = new FaixaPedida(new TrackReference(atual), foto.Atual.QuemPediu) { JáAnunciada = true };
        var posição = Retomável(atual, foto.Posição) ? foto.Posição : (TimeSpan?)null;

        await player.PlayAsync(
            item,
            enqueue: false,
            new TrackPlayProperties { StartPosition = posição },
            cancellationToken);

        if (foto.Pausado)
            await player.PauseAsync(cancellationToken);

        var fila = Fila(foto);

        if (fila.Count > 0)
            await player.Queue.AddRangeAsync(fila, cancellationToken);

        // a foto nova é do player vivo; a velha já cumpriu o papel
        player.Fotografar();

        _logger.LogInformation(
            "Voltei pro canal {Canal} de {Guild}: {Faixa} de {Posição}, mais {Fila} na fila",
            foto.CanalDeVoz, guild.Name, atual.Title, posição ?? TimeSpan.Zero, fila.Count);

        await player.FalarAsync(
            Music.EmbedVoltei(atual, posição, foto.Pausado, fila.Count),
            cancellationToken);
    }

    /// <summary>Seek só faz sentido em faixa que aceita e que não tinha acabado de começar.</summary>
    private static bool Retomável(LavalinkTrack faixa, TimeSpan posição) =>
        faixa.IsSeekable
        && !faixa.IsLiveStream
        && posição >= PosiçãoMínima
        && posição < faixa.Duration;

    /// <summary>A fila como estava; faixa que não decodifica mais é pulada, não derruba o resto.</summary>
    private List<ITrackQueueItem> Fila(PlayerSnapshot foto)
    {
        var fila = new List<ITrackQueueItem>(foto.Fila.Count);

        foreach (var guardada in foto.Fila)
        {
            if (LavalinkTrack.TryParse(guardada.Dados, null, out var faixa))
                fila.Add(new FaixaPedida(new TrackReference(faixa), guardada.QuemPediu));
            else
                _logger.LogWarning("Uma faixa da fila guardada da guild {Guild} não decodifica mais; pulei", foto.GuildId);
        }

        return fila;
    }

    private async Task<Guild?> EsperarGuildAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var limite = DateTimeOffset.UtcNow + EsperaPelaGuild;

        while (true)
        {
            if (_client.Cache.Guilds.TryGetValue(guildId, out var guild))
                return guild;

            if (DateTimeOffset.UtcNow >= limite)
                return null;

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    /// <summary>Alguém que não seja eu nem outro bot. Bot não ouve música, e eu muito menos.</summary>
    private bool AlguémNoCanal(Guild guild, ulong canalDeVoz)
    {
        foreach (var (userId, estado) in guild.VoiceStates)
        {
            if (estado.ChannelId != canalDeVoz || userId == _client.Id)
                continue;

            if (guild.Users.TryGetValue(userId, out var membro) && membro.IsBot)
                continue;

            return true;
        }

        return false;
    }

    /// <summary>O canal de texto onde o player falava. Apagado ou sem acesso: volto calada.</summary>
    private async Task<TextChannel?> CanalDeTextoAsync(ulong? id, CancellationToken cancellationToken)
    {
        if (id is null)
            return null;

        try
        {
            return await _client.Rest.GetChannelAsync(id.Value, cancellationToken: cancellationToken) as TextChannel;
        }
        catch (RestException erro)
        {
            _logger.LogWarning(erro, "Não achei mais o canal de texto {Canal}", id);
            return null;
        }
    }
}
