namespace AlephBot.Tests.Api;

// Um servidor de teste por classe: subir a API custa pouco, mas não precisa ser por teste.
public sealed class ApiFixture : IAsyncLifetime
{
    public ApiDeTeste Api { get; private set; } = null!;

    public async ValueTask InitializeAsync() => Api = await ApiDeTeste.SubirAsync();

    public ValueTask DisposeAsync() => Api.DisposeAsync();
}
