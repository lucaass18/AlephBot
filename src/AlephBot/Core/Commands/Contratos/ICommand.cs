namespace AlephBot.Core.Commands.Contratos;

public interface ICommand
{
    string Name { get; }
    
    string Description { get; }

    CommandCategory Category => CommandCategory.General;

    // Como se chama, sem o sinal: "mute <@usuário> <duração>". Quem exibe decide / ou !.
    string? Usage => null;
    
    bool IsHidden => false;
}