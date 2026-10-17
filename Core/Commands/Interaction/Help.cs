using AlephBot.Core.Commands.Interface;
using AlephBot.Core.Personality;

using NetCord;
using NetCord.Rest;

namespace AlephBot.Core.Commands.Interaction;

// Os embeds do /help e o menu que navega entre eles. Recebe a lista de comandos em vez do
// CommandRegistry porque assim o teste monta a lista na mão, sem precisar de DI.
internal static class Help
{
    // roxo da Denia, o mesmo do mute
    internal const int Roxo = 0x9B59B6;

    // Mensagem efêmera (/help): só quem pediu vê, então o clique pode editar no lugar.
    internal const string MenuId = "help-cat";

    // Mensagem pública (!help): editar seria mexer na mensagem de outro. Ver o módulo.
    internal const string MenuIdPúblico = "help-cat-pub";

    // o valor que traz de volta pro índice
    internal const string ValorDaCapa = "capa";

    // tetos do Discord
    private const int MaxDescrição = 4096;
    private const int MaxCampo = 1024;
    private const int MaxOpção = 100;

    // quantos nomes a capa adianta antes de contar o resto: mais que isso e a linha quebra
    private const int NomesNaCapa = 5;

    // ---- a capa --------------------------------------------------------------

    // O índice: uma linha por categoria com a conta e uma amostra dos nomes. A lista inteira
    // de cada categoria mora na página dela, não aqui — era o que fazia o embed virar paredão.
    internal static EmbedProperties Capa(IEnumerable<CommandInfo> visíveis, string prefixo)
    {
        var categorias = PorCategoria(visíveis);
        var total = categorias.Sum(c => c.Count);

        var corpo = categorias.Count == 0
            ? Denia.HelpVazio()
            : string.Join(
                "\n",
                [Denia.HelpIntro(total, prefixo), Denia.HelpDica(), "", .. categorias.Select(LinhaDaCapa)]);

        return new EmbedProperties
        {
            Title = Denia.HelpTítulo,
            Description = Recorta(corpo, MaxDescrição),
            Color = new Color(Roxo),
            Footer = new EmbedFooterProperties { Text = Denia.HelpRodapé() },
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    private static string LinhaDaCapa(Grupo categoria)
    {
        var amostra = string.Join(' ', categoria.Nomes.Take(NomesNaCapa).Select(n => $"`{n}`"));
        var resto = categoria.Count - NomesNaCapa;

        // o "+n" sai sem crase de propósito: com crase o olho lê como se fosse mais um comando
        if (resto > 0)
            amostra = $"{amostra} +{resto}";

        var título = $"**{Denia.HelpCategoria(categoria.Chave)}** · {categoria.Count}";

        // categoria de um ou dois comandos cabe na própria linha; quebrar só gastaria espaço
        return categoria.Count <= 2 ? $"{título} · {amostra}" : $"{título}\n{amostra}";
    }

    // ---- a página de uma categoria -------------------------------------------

    // Null quando o valor não é categoria nenhuma, ou é uma que ficou vazia: menu de uma
    // versão antiga do bot ainda clica, e quem clicou não merece um erro na cara.
    internal static EmbedProperties? Categoria(IEnumerable<CommandInfo> visíveis, string valor)
    {
        if (!Enum.TryParse<CommandCategory>(valor, ignoreCase: true, out var chave))
            return null;

        var comandos = visíveis
            .Where(c => c.Category == chave)
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToList();

        if (comandos.Count == 0)
            return null;

        var corpo = $"{Denia.HelpCategoriaIntro(comandos.Count)}\n\n{string.Join('\n', comandos.Select(Linha))}";

        return new EmbedProperties
        {
            Title = Denia.HelpCategoria(chave),
            Description = Recorta(corpo, MaxDescrição),
            Color = new Color(Roxo),
            Footer = new EmbedFooterProperties { Text = Denia.HelpRodapé() },
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    private static string Linha(CommandInfo comando) =>
        $"`/{comando.Name}` — {comando.Description}";

    // ---- o menu --------------------------------------------------------------

    // `selecionada` marca a categoria aberta, pra lista não voltar sempre no placeholder.
    internal static StringMenuProperties Menu(
        IEnumerable<CommandInfo> visíveis, string id, CommandCategory? selecionada = null)
    {
        List<StringMenuSelectOptionProperties> opções =
        [
            new(Denia.HelpMenuCapa, ValorDaCapa)
            {
                Description = Denia.HelpMenuCapaDescrição,
                Default = selecionada is null,
            },
        ];

        opções.AddRange(PorCategoria(visíveis).Select(categoria =>
            new StringMenuSelectOptionProperties(
                Denia.HelpCategoria(categoria.Chave),
                categoria.Chave.ToString())
            {
                Description = Encurta(Denia.HelpMenuDescrição(categoria.Count, categoria.Nomes), MaxOpção),
                Default = selecionada == categoria.Chave,
            }));

        return new StringMenuProperties(id, opções) { Placeholder = Denia.HelpMenuRótulo };
    }

    // A página que o valor escolhido no menu pede, com a categoria que fica marcada nele.
    // Valor desconhecido - menu de uma versão antiga, categoria que saiu do bot - cai na capa,
    // porque quem clicou não tem culpa de o bot ter mudado embaixo dele.
    internal static (EmbedProperties Embed, CommandCategory? Selecionada) Página(
        IEnumerable<CommandInfo> visíveis, string prefixo, string? escolha)
    {
        if (escolha is not null and not ValorDaCapa
            && Categoria(visíveis, escolha) is { } embed
            && Enum.TryParse<CommandCategory>(escolha, ignoreCase: true, out var categoria))
        {
            return (embed, categoria);
        }

        return (Capa(visíveis, prefixo), null);
    }

    // ---- o detalhe de um comando ---------------------------------------------

    // O que já existia, intocado no conteúdo: só mudou de casa.
    internal static EmbedProperties? Detalhe(IEnumerable<CommandInfo> todos, string prefixo, string comando)
    {
        var busca = Normaliza(comando, prefixo);

        // slash e prefixo são classes com o mesmo Name, e o "Como chamar" mostra as duas
        var entradas = todos
            .Where(c => string.Equals(c.Name, busca, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (entradas.Count == 0)
            return null;

        // a versão visível é a que descreve o comando; a escondida é só o gêmeo de prefixo
        var principal = entradas.FirstOrDefault(c => !c.IsHidden) ?? entradas[0];

        var usos = entradas
            .Select(c => c.Uso(prefixo))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var atalhos = entradas
            .SelectMany(c => CommandAudit.AliasesDeTexto(c.Type))
            .Where(a => !string.Equals(a, principal.Name, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<EmbedFieldProperties> campos =
        [
            new()
            {
                // Uso nunca vem vazio (no pior caso cai no nome), então não trato ausência
                Name = Denia.HelpCampoUso,
                Value = Recorta(string.Join('\n', usos.Select(u => $"`{u}`")), MaxCampo),
            },
            new()
            {
                Name = Denia.HelpCampoCategoria,
                Value = Denia.HelpCategoria(principal.Category),
                Inline = true,
            },
        ];

        if (atalhos.Count > 0)
        {
            campos.Add(new EmbedFieldProperties
            {
                Name = Denia.HelpCampoAtalhos,
                Value = Recorta(string.Join(" ", atalhos.Select(a => $"`{prefixo}{a}`")), MaxCampo),
                Inline = true,
            });
        }

        return new EmbedProperties
        {
            Title = Denia.HelpDetalheTítulo($"/{principal.Name}"),
            Description = principal.Description,
            Color = new Color(Roxo),
            Fields = campos,
            Footer = new EmbedFooterProperties { Text = Denia.Assinatura },
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    // Aceita "mute", "/mute" e "!mute" — quem pede ajuda costuma colar o comando inteiro.
    internal static string Normaliza(string comando, string prefixo)
    {
        var limpo = comando.Trim();

        // prefixo configurado pode ser uma palavra ("bot "), então sai primeiro
        if (limpo.StartsWith(prefixo, StringComparison.Ordinal))
            limpo = limpo[prefixo.Length..];

        // nome de comando não começa com sinal: o que vem antes da primeira letra é enfeite
        var início = 0;

        while (início < limpo.Length && !char.IsLetterOrDigit(limpo[início]))
            início++;

        return limpo[início..].Trim();
    }

    // ---- miudezas ------------------------------------------------------------

    // Uma categoria já contada e ordenada: a capa e o menu pedem as mesmas três coisas.
    private readonly record struct Grupo(CommandCategory Chave, int Count, IReadOnlyList<string> Nomes);

    private static List<Grupo> PorCategoria(IEnumerable<CommandInfo> visíveis) =>
        [.. visíveis
            .GroupBy(c => c.Category)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var nomes = g.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

                return new Grupo(g.Key, nomes.Count, nomes);
            })];

    // Corta no fim da última linha inteira: embed estourado o Discord recusa.
    private static string Recorta(string texto, int limite)
    {
        if (texto.Length <= limite)
            return texto;

        var corte = texto.LastIndexOf('\n', limite - 2);

        return corte > 0 ? $"{texto[..corte]}\n…" : $"{texto[..(limite - 1)]}…";
    }

    // A descrição de uma opção do menu é uma linha só, então aqui o corte é seco.
    private static string Encurta(string texto, int limite) =>
        texto.Length <= limite ? texto : $"{texto[..(limite - 1)]}…";
}
