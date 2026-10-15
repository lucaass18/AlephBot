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
    // Pasta onde a classe mora — último trecho do namespace, ex.: "Admin".
    public string Pasta => Type.Namespace?.Split('.')[^1] ?? "?";

    // Se este é o gêmeo de prefixo e não o de barra. Quem responde é a classe base, não o Usage.
    public bool IsTexto => typeof(AlephTextModule).IsAssignableFrom(Type);

    // O Usage com o sinal na frente. O prefixo vem da config, então mexer no PREFIX muda tudo.
    public string Uso(string prefixo) =>
        $"{(IsTexto ? prefixo : "/")}{Usage ?? Name}";
}
