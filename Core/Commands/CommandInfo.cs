using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Interface;

namespace AlephBot.Core.Commands;

public sealed record CommandInfo(
    string Name,
    string Description,
    CommandCategory Category,
    string? Usage,
    bool IsHidden,
    Type Type)
{
    /// <summary>Pasta onde a classe mora — último trecho do namespace, ex.: "Admin".</summary>
    public string Pasta => Type.Namespace?.Split('.')[^1] ?? "?";

    /// <summary>
    /// Se este é o gêmeo de prefixo (!comando) e não o de barra (/comando).
    /// Quem responde é a classe base, não o texto do Usage — o prefixo pode ser qualquer um.
    /// </summary>
    public bool IsTexto => typeof(AlephTextModule).IsAssignableFrom(Type);

    /// <summary>
    /// O Usage pronto pra mostrar, com o sinal na frente: "/mute ..." ou "!mute ...".
    /// O prefixo vem da configuração, então trocar PREFIX no .env muda tudo que o
    /// usuário lê — ajuda, mensagens de erro e o log do boot.
    /// </summary>
    public string Uso(string prefixo) =>
        $"{(IsTexto ? prefixo : "/")}{Usage ?? Name}";
}
