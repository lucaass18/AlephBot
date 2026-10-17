using AlephBot.Core.Commands.Interaction;

using Microsoft.Extensions.DependencyInjection;

using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;

namespace AlephBot.Tests.Commands;

// O NetCord só consulta módulo cujo contexto tem serviço registrado. Módulo escrito sobre um
// contexto que ninguém registrou compila, sobe, e só aparece no clique: "Component interaction
// not found". Foi o que aconteceu com o menu do /help, e é o que este teste não deixa voltar.
public class ComponentesTests
{
    // o mesmo AddComponentInteractions() da inicialização do bot
    private static Type ContextoRegistrado()
    {
        var serviços = new ServiceCollection();
        serviços.AddComponentInteractions();

        var contextos = serviços
            .Select(d => d.ServiceType)
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ComponentInteractionService<>))
            .Select(t => t.GetGenericArguments()[0])
            .Distinct()
            .ToList();

        return Assert.Single(contextos);
    }

    private static List<Type> MódulosDeComponente() =>
        [.. typeof(HelpNavegaçãoModule).Assembly.GetTypes()
            .Where(tipo => tipo is { IsClass: true, IsAbstract: false }
                           && tipo.BaseType is { IsGenericType: true } mãe
                           && mãe.GetGenericTypeDefinition() == typeof(ComponentInteractionModule<>))];

    [Fact]
    public void Todo_módulo_de_componente_nasce_no_contexto_que_o_bot_registra()
    {
        var esperado = ContextoRegistrado();
        var módulos = MódulosDeComponente();

        Assert.NotEmpty(módulos);

        Assert.All(módulos, tipo => Assert.Equal(
            esperado,
            tipo.BaseType!.GetGenericArguments()[0]));
    }
}
