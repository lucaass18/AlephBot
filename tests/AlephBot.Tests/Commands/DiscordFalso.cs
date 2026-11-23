using NetCord;
using NetCord.Gateway;
using NetCord.JsonModels;
using NetCord.Rest;

namespace AlephBot.Tests.Commands;

// Servidor, cargos e membros do Discord montados na memória, sem conexão nenhuma.
// Servem às funções que só leem os dados (ids, cargos, posições) e nunca chamam a API.
internal static class DiscordFalso
{
    private static readonly RestClient Rest = new();

    public const ulong ServidorId = 1;
    public const ulong DonoId = 100;
    public const ulong BotId = 999;

    // cargos: id → posição na lista do servidor (maior = mais alto)
    public static Guild Servidor(params (ulong Id, int Posição)[] cargos) =>
        Criar(new JsonGuild
        {
            Id = ServidorId,
            OwnerId = DonoId,
            Roles = [.. cargos.Select(c => new JsonRole { Id = c.Id, Position = c.Posição, Name = $"cargo {c.Id}" })],
        });
    private static Guild Criar(JsonGuild json) => new(json, BotId, Rest, new Dicionários());

    public static GuildUser Membro(ulong id, params ulong[] cargos) => Membro(id, null, cargos);

    public static GuildUser Membro(ulong id, DateTimeOffset? silenciadoAté, params ulong[] cargos) =>
        new(new JsonGuildUser
        {
            User = new JsonUser { Id = id, Username = $"usuario{id}" },
            RoleIds = cargos,
            TimeOutUntil = silenciadoAté,
        }, ServidorId, Rest);

    // o NetCord pede quem monta os dicionários de cargos e canais; o dele é interno
    private sealed class Dicionários : IDictionaryProvider
    {
        public IDictionaryProvider OfDictionary => this;

        IReadOnlyDictionary<TKey, TElement> IDictionaryProvider.CreateDictionary<TSource, TKey, TElement>(
            IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector) =>
            source.ToDictionary(keySelector, elementSelector);
    }
}
