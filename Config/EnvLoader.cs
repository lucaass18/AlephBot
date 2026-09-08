namespace AlephBot.Config;

public static class EnvLoader
{
    private const string Template =
        """
        # discord
        TOKEN=
        PREFIX=!
        DEV_GUILD_ID=

        # Geral
        LOG_LEVEL=Information

        # musica (Lavalink) — o application.yml do repositorio ja usa estes valores
        LAVALINK_URI=http://localhost:2333/
        LAVALINK_PASSWORD=youshallnotpass
        MUSIC_IDLE_MINUTES=2
        """;

    public static void Load(string fileName = ".env")
    {
        var path = Resolve(fileName);
        if (path is null)
            return; // em produção as vars vêm do container, então não é erro

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line[7..].TrimStart();

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            // remove aspas envolventes, se houver
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            // não sobrescreve o que já veio do ambiente real
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>
    /// Cria o arquivo com o template, se faltar. Devolve o caminho, ou null quando já
    /// existia ou não é um checkout do projeto (container publicado, por exemplo).
    /// </summary>
    public static string? CreateIfMissing(string fileName = ".env")
    {
        if (Resolve(fileName) is not null)
            return null;

        var root = FindProjectRoot();
        if (root is null)
            return null;

        var path = Path.Combine(root, fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Template + Environment.NewLine);

        return path;
    }

    private static string? Resolve(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        // sobe até achar (útil no bin/Debug/netX.0 durante o dev)
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        return File.Exists(fileName) ? Path.GetFullPath(fileName) : null;
    }

    /// <summary>Sobe a árvore procurando a pasta que contém o .csproj.</summary>
    private static string? FindProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (dir.EnumerateFiles("*.csproj").Any())
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }
}
