namespace AlephBot.Core.Commands.Interface;

public interface ICommand
{
    string Name { get; }
    
    string Description { get; }

    CommandCategory Category => CommandCategory.General;

    /// <summary>
    /// Como se chama o comando, sem o sinal na frente: "mute &lt;@usuário&gt; &lt;duração&gt;".
    /// Quem exibe é que decide se vira "/mute" ou "!mute" — ver CommandInfo.Uso.
    /// </summary>
    string? Usage => null;
    
    bool IsHidden => false;
}