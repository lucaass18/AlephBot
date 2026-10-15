using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Interface;
using AlephBot.Core.Personality;

using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Admin;

// /clear — apaga as últimas mensagens do canal e diz em embed o que saiu e o que ficou.
public sealed class ClearCommand : AlephSlashModule
{
    public override string Name => "clear";
    public override string Description => "Apaga as últimas mensagens do canal.";
    public override CommandCategory Category => CommandCategory.Moderation;
    public override string? Usage => Limpeza.Uso;

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    [RequireBotPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    [SlashCommand("clear", "Apaga as últimas mensagens do canal.")]
    public async Task ClearAsync(
        [SlashCommandParameter(Name = "quantidade", Description = "Quantas mensagens apagar (1 a 100).")]
        int quantidade)
    {
        if (Limpeza.Recusa(quantidade) is { } erro)
        {
            await ErrorAsync(erro);
            return;
        }

        // ler o canal e apagar são dois pedidos ao Discord; isso passa dos 3 segundos da interação
        await DeferAsync();

        var (saldo, falha) = await Limpeza.ApagarAsync(Context.Client.Rest, Context.Channel.Id, quantidade);

        if (falha is not null)
            await EditarComErroAsync(falha);
        else
            await EditarRespostaAsync(Limpeza.Embed(saldo, Context.User.Username));
    }
}

// !clear — a mesma faxina do /clear, para quem prefere o prefixo.
public sealed class ClearTextCommand : AlephTextModule
{
    public override string Name => "clear";
    public override string Description => "Apaga as últimas mensagens do canal.";
    public override CommandCategory Category => CommandCategory.Moderation;
    public override string? Usage => Limpeza.Uso;

    // escondido do /help pra não duplicar a entrada do slash
    public override bool IsHidden => true;

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<CommandContext>(Permissions.ManageMessages)]
    [RequireBotPermissions<CommandContext>(Permissions.ManageMessages)]
    [Command("clear", "limpar")]
    public async Task ClearAsync(int quantidade)
    {
        if (Limpeza.Recusa(quantidade) is { } erro)
        {
            await ErrorAsync(erro);
            return;
        }

        // o pedido também é bagunça: sai junto, sem entrar na conta de quem pediu
        var (saldo, falha) = await Limpeza.ApagarAsync(
            Context.Client.Rest, Context.Message.ChannelId, quantidade, oComando: Context.Message.Id);

        if (falha is not null)
        {
            await ErrorAsync(falha);
            return;
        }

        // responder não dá: a mensagem que eu responderia acabou de sair junto
        await Context.Client.Rest.SendMessageAsync(
            Context.Message.ChannelId,
            new MessageProperties { Embeds = [Limpeza.Embed(saldo, Context.User.Username)] });
    }
}

// A faxina em si: ler o canal, separar o que dá pra apagar e contar o que ficou pra trás.
internal static class Limpeza
{
    internal const string Uso = "clear <quantidade>";

    internal const int Mínimo = 1;

    // Teto do Discord para apagar em lote de uma vez.
    internal const int Máximo = 100;

    // Mais velha que isso o Discord recusa em lote, e uma a uma seria lento e barulhento.
    internal static readonly TimeSpan Idade = TimeSpan.FromDays(14);

    // cinza de vassoura: não é punição, e também não é notícia boa
    private const int Cinza = 0x95A5A6;

    internal static string? Recusa(int quantidade) =>
        quantidade is >= Mínimo and <= Máximo ? null : Denia.ClearQuantidadeInválida(Mínimo, Máximo);

    // O que saiu, e o que ficou porque eu não podia tirar.
    internal readonly record struct Saldo(int Apagadas, int Fixadas, int Velhas);

    internal static async Task<(Saldo Saldo, string? Falha)> ApagarAsync(
        RestClient rest, ulong canal, int quantidade, ulong? oComando = null)
    {
        var lidas = new List<(ulong Id, bool Fixada)>();

        // do comando pra trás: ele é o mais novo do canal e não deve ocupar uma das vagas
        var paginação = oComando is { } pedido
            ? new PaginationProperties<ulong> { From = pedido, Direction = PaginationDirection.Before }
            : null;

        try
        {
            await foreach (var mensagem in rest.GetMessagesAsync(canal, paginação))
            {
                lidas.Add((mensagem.Id, mensagem.IsPinned));

                if (lidas.Count == quantidade)
                    break;
            }

            var (ids, saldo) = Separa(lidas, DateTimeOffset.UtcNow);

            if (oComando is { } comando)
                ids.Add(comando);

            // o lote pede dois ou mais; com um só o Discord quer a rota de apagar uma
            if (ids.Count >= 2)
                await rest.DeleteMessagesAsync(canal, ids);
            else if (ids.Count == 1)
                await rest.DeleteMessageAsync(canal, ids[0]);

            return (saldo, null);
        }
        catch (RestException erro)
        {
            return (default, Denia.ClearFalhou(erro.StatusCode.ToString()));
        }
    }

    // Fixada fica onde está, velha o Discord não deixa; o resto vai embora.
    internal static (List<ulong> Ids, Saldo Saldo) Separa(
        IReadOnlyList<(ulong Id, bool Fixada)> mensagens, DateTimeOffset agora)
    {
        List<ulong> ids = [];
        var fixadas = 0;
        var velhas = 0;

        foreach (var (id, fixada) in mensagens)
        {
            if (fixada)
                fixadas++;
            else if (agora - Snowflake.Timestamp(id) >= Idade)
                velhas++;
            else
                ids.Add(id);
        }

        return (ids, new Saldo(ids.Count, fixadas, velhas));
    }

    internal static EmbedProperties Embed(Saldo saldo, string quem)
    {
        List<EmbedFieldProperties> campos = [];

        // campo vazio é sujeira: o que não ficou pra trás não vira linha no embed
        if (saldo.Fixadas > 0)
            campos.Add(Campo(Denia.ClearCampoFixadas, Denia.ClearFixadas(saldo.Fixadas)));

        if (saldo.Velhas > 0)
            campos.Add(Campo(Denia.ClearCampoVelhas, Denia.ClearVelhas(saldo.Velhas)));

        return new EmbedProperties
        {
            Title = Denia.ClearTítulo,
            Description = Denia.ClearApagadas(saldo.Apagadas),
            Color = new Color(Cinza),
            Fields = campos,
            Footer = new EmbedFooterProperties { Text = Denia.Rodapé(quem) },
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    private static EmbedFieldProperties Campo(string nome, string valor) =>
        new() { Name = nome, Value = valor, Inline = true };
}
