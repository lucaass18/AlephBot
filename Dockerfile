# aspnet e nao runtime: a API de status roda no Kestrel, dentro do mesmo processo do bot
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
# os volumes de logs e de dados precisam existir e ser do usuario do app antes do USER,
# senao o Docker cria o mount como root e nem o NLog nem o token do YouTube escrevem
RUN mkdir -p /app/logs /app/data && chown -R $APP_UID /app/logs /app/data
USER $APP_UID
# so escuta com API_KEY no Config/.env; sem ela a porta fica fechada
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AlephBot.csproj", "./"]
RUN dotnet restore "AlephBot.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "./AlephBot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AlephBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AlephBot.dll"]
