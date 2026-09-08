using AlephBot.Config;

namespace AlephBot.Threnodian;

public static class Program
{
    public static Task Main(string[] args) =>
        new AlephBot(AlephConfig.Load()).RunAsync(args);
}