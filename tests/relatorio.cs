#:project ../AlephBot.csproj
#:property PublishAot=false

// Roda os testes de unidade e mostra o resultado na paleta do console do bot, agrupado por
// pasta e classe. O que vier depois do "--" vai direto pro dotnet test:
//
//   dotnet run tests/relatorio.cs
//   dotnet run tests/relatorio.cs -- --filter-class AlephBot.Tests.Commands.MuteCommandTests

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using AlephBot.Core.Personality;

Console.OutputEncoding = Encoding.UTF8;

var cor = Banner.UsaCor();
string Pinta(string código) => cor ? código : "";

var (roxo, cinza, apagado, laranja, vermelho, fim) = (
    Pinta(Banner.Roxo), Pinta(Banner.Cinza), Pinta(Banner.Apagado),
    Pinta(Banner.Laranja), Pinta(Banner.Vermelho), Pinta(Banner.Fim));

var raiz = Path.GetDirectoryName(Script.Pasta())!;
var resultados = Path.Combine(Path.GetTempPath(), $"alephbot-testes-{Guid.NewGuid():N}");
var xml = Path.Combine(resultados, "resultado.xml");

Console.WriteLine();
Console.WriteLine($"  {roxo}Denia{fim} {cinza}· {Denia.TestesTítulo}{fim}");
Console.WriteLine($"  {apagado}{Denia.TestesRodando()}{fim}");
Console.WriteLine();

var (código, saída) = await Script.RodarAsync(raiz, "dotnet",
[
    "test", "--project", Path.Combine("tests", "AlephBot.Tests"),
    "--results-directory", resultados,
    "--report-xunit-xml", "--report-xunit-xml-filename", "resultado.xml",
    "--progress", "off", "--no-ansi",
    .. args,
]);

// sem relatório o problema veio antes dos testes (quase sempre o build): o que o dotnet disse é o que importa
if (!File.Exists(xml))
{
    Console.WriteLine($"  {cinza}{Denia.TestesNãoRodaram()}{fim}");
    Console.WriteLine();
    Console.WriteLine($"{vermelho}{saída.Trim()}{fim}");
    Console.WriteLine();
    return código == 0 ? 1 : código;
}

var documento = XDocument.Load(xml);
Directory.Delete(resultados, recursive: true);

var testes = documento.Descendants("test").Select(Teste.De).ToList();

if (testes.Count == 0)
{
    Console.WriteLine($"  {laranja}{Denia.TestesNenhum()}{fim}");
    Console.WriteLine();
    return 1;
}

// ---- a árvore: pasta, classe e, quando quebra, o teste que quebrou ---------------

var largura = testes.Max(t => t.Classe.Length) + 8;

foreach (var pasta in testes.GroupBy(t => t.Pasta).OrderBy(g => g.Key, StringComparer.Ordinal))
{
    Linha(pasta.Key, [.. pasta], recuo: 2, corDoNome: "", corDaConta: cinza);

    foreach (var classe in pasta.GroupBy(t => t.Classe).OrderBy(g => g.Key, StringComparer.Ordinal))
    {
        Linha(classe.Key, [.. classe], recuo: 6, corDoNome: cinza, corDaConta: apagado);

        foreach (var falha in classe.Where(t => t.Falhou))
            Falha(falha);
    }
}

// ---- o resumo e o veredito dela -------------------------------------------------

var passaram = testes.Count(t => t.Passou);
var falharam = testes.Count(t => t.Falhou);
var ignorados = testes.Count(t => t.Ignorado);

var segundos = double.TryParse(
    (string?)documento.Descendants("assembly").FirstOrDefault()?.Attribute("time"),
    NumberStyles.Float, CultureInfo.InvariantCulture, out var tempo) ? tempo : testes.Sum(t => t.Segundos);

var separador = $"{cinza}  ·  {fim}";

Console.WriteLine();
Console.WriteLine(
    $"  {roxo}{passaram} {(passaram == 1 ? "passou" : "passaram")}{fim}{separador}" +
    $"{(falharam > 0 ? vermelho : cinza)}{falharam} {(falharam == 1 ? "falhou" : "falharam")}{fim}{separador}" +
    $"{(ignorados > 0 ? laranja : cinza)}{ignorados} {(ignorados == 1 ? "ignorado" : "ignorados")}{fim}{separador}" +
    $"{cinza}{segundos.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR"))} s{fim}");

Console.WriteLine($"  {cinza}{(falharam > 0 ? Denia.TestesFalharam(falharam, testes.Count) : Denia.TestesPassaram(testes.Count))}{fim}");
Console.WriteLine();

return falharam > 0 ? 1 : 0;

void Linha(string nome, List<Teste> grupo, int recuo, string corDoNome, string corDaConta)
{
    var falhas = grupo.Count(t => t.Falhou);

    var (símbolo, corDoSímbolo) =
        falhas > 0 ? ("✘", vermelho)
        : grupo.All(t => t.Ignorado) ? ("○", laranja)
        : ("✔", roxo);

    var conta = falhas > 0 ? $"{grupo.Count - falhas}/{grupo.Count}" : $"{grupo.Count}";

    Console.WriteLine(
        $"{new string(' ', recuo)}{corDoSímbolo}{símbolo}{fim} " +
        $"{corDoNome}{nome.PadRight(largura - recuo)}{fim}{corDaConta}{conta,7}{fim}");
}

void Falha(Teste teste)
{
    Console.WriteLine($"          {vermelho}✘ {teste.NomeLegível}{fim}");

    foreach (var linha in teste.Mensagem.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).Take(8))
        Console.WriteLine($"            {cinza}{linha}{fim}");

    if (teste.Onde is { } onde)
        Console.WriteLine($"            {apagado}{onde}{fim}");
}

/// <summary>Um teste como o xUnit escreve no relatório XML.</summary>
sealed partial record Teste(
    string Tipo, string Nome, string Resultado, double Segundos, string Mensagem, string? Pilha, string? Arquivo, string? Linha)
{
    private const string Prefixo = "AlephBot.Tests.";

    public static Teste De(XElement teste) => new(
        (string?)teste.Attribute("type") ?? "?",
        (string?)teste.Attribute("name") ?? "?",
        (string?)teste.Attribute("result") ?? "?",
        double.TryParse((string?)teste.Attribute("time"), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 0,
        Desescapa((string?)teste.Element("failure")?.Element("message") ?? ""),
        (string?)teste.Element("failure")?.Element("stack-trace"),
        (string?)teste.Attribute("source-file"),
        (string?)teste.Attribute("source-line"));

    public bool Passou => Resultado == "Pass";

    public bool Falhou => Resultado == "Fail";

    public bool Ignorado => Resultado is "Skip" or "NotRun";

    /// <summary>"AlephBot.Tests.Commands.MuteCommandTests" mora na pasta "Commands".</summary>
    public string Pasta => Tipo.StartsWith(Prefixo, StringComparison.Ordinal) ? Tipo[Prefixo.Length..].Split('.')[0] : "?";

    public string Classe => Tipo[(Tipo.LastIndexOf('.') + 1)..];

    /// <summary>"Senha_errada_não_abre(tentativa: \"x\")" vira "Senha errada não abre (tentativa: "x")".</summary>
    public string NomeLegível
    {
        get
        {
            var nome = Desescapa(Nome.StartsWith(Tipo + ".", StringComparison.Ordinal) ? Nome[(Tipo.Length + 1)..] : Nome);

            var parêntese = nome.IndexOf('(');

            return parêntese < 0
                ? nome.Replace('_', ' ')
                : $"{nome[..parêntese].Replace('_', ' ')} {nome[parêntese..]}";
        }
    }

    /// <summary>Onde o Assert estourou — a primeira linha da pilha que cai num .cs — ou, sem pilha, o teste.</summary>
    public string? Onde =>
        Pilha is not null && LinhaDaPilha().Match(Pilha) is { Success: true } achado
            ? $"{Path.GetFileName(achado.Groups["arquivo"].Value)}:{achado.Groups["linha"].Value}"
            : Arquivo is not null ? $"{Path.GetFileName(Arquivo)}:{Linha}" : null;

    /// <summary>O relatório XML escapa aspas e barras mais uma vez por cima do que o xUnit escreveu.</summary>
    private static string Desescapa(string texto) => Escapado().Replace(texto, "$1");

    // "em X.cs:linha 41", "in X.cs:line 41" e o que mais o idioma do sistema escrever no meio
    [GeneratedRegex(@"(?<arquivo>[^\s]+\.cs):(?:\p{L}+ )?(?<linha>\d+)")]
    private static partial Regex LinhaDaPilha();

    [GeneratedRegex(@"\\([\\""])")]
    private static partial Regex Escapado();
}

static class Script
{
    /// <summary>A pasta deste arquivo, gravada na compilação: o script acha o repositório de onde quer que rode.</summary>
    public static string Pasta([CallerFilePath] string arquivo = "") => Path.GetDirectoryName(arquivo)!;

    public static async Task<(int Código, string Saída)> RodarAsync(string pasta, string programa, IEnumerable<string> argumentos)
    {
        var início = new ProcessStartInfo(programa)
        {
            // da raiz, pro dotnet test achar o global.json que liga a Microsoft.Testing.Platform
            WorkingDirectory = pasta,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argumento in argumentos)
            início.ArgumentList.Add(argumento);

        using var processo = Process.Start(início)!;

        var saída = processo.StandardOutput.ReadToEndAsync();
        var erro = processo.StandardError.ReadToEndAsync();

        await processo.WaitForExitAsync();

        return (processo.ExitCode, await saída + await erro);
    }
}
