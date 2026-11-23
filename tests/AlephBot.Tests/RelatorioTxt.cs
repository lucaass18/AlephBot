using System.Globalization;
using System.Reflection;
using System.Text;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Services;

namespace AlephBot.Tests;

// Extensão do Microsoft Testing Platform: todo `dotnet test` também deixa um relatorio_*.txt na
// TestResults da raiz, no mesmo formato do tests/relatorio.cs. Registrada pelo
// <TestingPlatformBuilderHook> do .csproj; o tests/relatorio.cs desliga com -p:RelatorioNaRaiz=false.
public static class RelatorioTxtHook
{
    public static void AddExtensions(ITestApplicationBuilder builder, string[] _)
    {
        var fábrica = new CompositeExtensionFactory<RelatorioTxt>(() => new RelatorioTxt());
        builder.TestHost.AddDataConsumer(fábrica);
        builder.TestHost.AddTestSessionLifetimeHandler(fábrica);
    }
}

public sealed class RelatorioTxt : IDataConsumer, ITestSessionLifetimeHandler, IAsyncDisposable
{
    private sealed record Resultado(string Pasta, string Classe, string Nome, char Estado, string? Mensagem);

    private readonly Dictionary<string, Resultado> resultados = [];
    private DateTime início = DateTime.Now;

    private static readonly string? Pasta = typeof(RelatorioTxt).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "RelatorioTxtPasta")?.Value;

    public string Uid => nameof(RelatorioTxt);
    public string Version => "1.0.0";
    public string DisplayName => "Relatório em .txt";
    public string Description => "Salva o resultado dos testes num .txt na TestResults da raiz.";
    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(!string.IsNullOrEmpty(Pasta));

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (value is not TestNodeUpdateMessage { TestNode: var nó })
            return Task.CompletedTask;

        var estado = nó.Properties.SingleOrDefault<TestNodeStateProperty>();
        (char símbolo, string? mensagem) = estado switch
        {
            PassedTestNodeStateProperty => ('✔', null),
            SkippedTestNodeStateProperty s => ('○', s.Explanation),
            FailedTestNodeStateProperty f => ('✘', f.Exception?.ToString() ?? f.Explanation),
            ErrorTestNodeStateProperty e => ('✘', e.Exception?.ToString() ?? e.Explanation),
            TimeoutTestNodeStateProperty t => ('✘', t.Explanation ?? "estourou o tempo"),
            _ => ('\0', null),
        };

        if (símbolo == '\0')
            return Task.CompletedTask;

        var método = nó.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
        var ns = método?.Namespace ?? "";
        var pasta = ns.StartsWith("AlephBot.Tests.", StringComparison.Ordinal) ? ns["AlephBot.Tests.".Length..]
                  : ns == "AlephBot.Tests" ? "(raiz)" : ns;

        lock (resultados)
            resultados[$"{nó.Uid.Value}|{nó.DisplayName}"] = new(pasta, método?.TypeName ?? "?", Legível(nó.DisplayName, método), símbolo, mensagem);

        return Task.CompletedTask;
    }

    // "AlephBot.Tests.Api.X.Nome_do_teste(1)" vira "Nome do teste(1)"
    private static string Legível(string nome, TestMethodIdentifierProperty? método)
    {
        if (método is not null && nome.StartsWith($"{método.Namespace}.{método.TypeName}.", StringComparison.Ordinal))
            nome = nome[($"{método.Namespace}.{método.TypeName}.".Length)..];
        return nome.Replace('_', ' ');
    }

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        início = DateTime.Now;
        return Task.CompletedTask;
    }

    public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        fim = DateTime.Now;
        return Task.CompletedTask;
    }

    // as mensagens chegam numa fila em segundo plano; só no descarte da extensão todas já passaram
    public ValueTask DisposeAsync()
    {
        Escreve();
        return ValueTask.CompletedTask;
    }

    private DateTime fim;

    private void Escreve()
    {
        List<Resultado> testes;
        lock (resultados)
            testes = [.. resultados.Values];

        if (testes.Count == 0 || Pasta is null)
            return;

        var segundos = (fim - início).TotalSeconds.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR"));
        var texto = new StringBuilder();

        texto.AppendLine($"AlephBot · relatório dos testes · {início:dd/MM/yyyy HH:mm:ss}");
        texto.AppendLine(
            $"{testes.Count(t => t.Estado == '✔')} passaram · {testes.Count(t => t.Estado == '✘')} falharam · " +
            $"{testes.Count(t => t.Estado == '○')} ignorados · {segundos} s");

        foreach (var pasta in testes.GroupBy(t => t.Pasta).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            texto.AppendLine();
            texto.AppendLine($"=== {pasta.Key} ({pasta.Count()})");

            foreach (var classe in pasta.GroupBy(t => t.Classe).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                texto.AppendLine();
                texto.AppendLine($"  {classe.Key} ({classe.Count(t => t.Estado == '✔')}/{classe.Count()})");

                foreach (var teste in classe.OrderBy(t => t.Nome, StringComparer.Ordinal))
                {
                    texto.AppendLine($"    {teste.Estado} {teste.Nome}");

                    if (teste.Estado != '✘' || teste.Mensagem is null)
                        continue;

                    foreach (var linha in teste.Mensagem.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0))
                        texto.AppendLine($"        {linha}");
                }
            }
        }

        Directory.CreateDirectory(Pasta);
        var arquivo = Path.Combine(Pasta, $"relatorio_{início:yyyy-MM-dd_HH-mm-ss}.txt");
        File.WriteAllText(arquivo, texto.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}
