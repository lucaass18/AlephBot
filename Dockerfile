FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app
# o volume de logs precisa existir e ser do usuario do app antes do USER,
# senao o Docker cria o mount como root e o target File do NLog nao escreve
RUN mkdir -p /app/logs && chown -R $APP_UID /app/logs
USER $APP_UID

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
