# =============================================================================
# MoveSync API (.NET 10) — imagem de produção em 2 estágios
#   1) build: restaura pacotes, roda os testes unitários e publica em Release
#   2) runtime: só o ASP.NET Core runtime + a aplicação publicada (imagem menor)
# Build (na raiz do repositório):
#   docker build -f dockerfiles/Dockerfile -t movesync-api .
# Execução (variáveis de ambiente — nunca coloque segredos na imagem):
#   docker run -p 8080:8080 \
#     -e ConnectionStrings__MoveSync="User Id=...;Password=...;Data Source=oracle.fiap.com.br:1521/ORCL" \
#     -e Jwt__Chave="<chave com 32+ caracteres>" \
#     -e Seed__GestorSenha="..." -e Seed__CidadaoSenha="..." -e Seed__SegredoSensores="..." \
#     movesync-api
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia primeiro só os arquivos de projeto: o "restore" fica em cache entre builds.
COPY global.json Directory.Build.props Directory.Packages.props MoveSync.sln ./
COPY src/MoveSync.Domain/MoveSync.Domain.csproj src/MoveSync.Domain/
COPY src/MoveSync.Application/MoveSync.Application.csproj src/MoveSync.Application/
COPY src/MoveSync.Infrastructure/MoveSync.Infrastructure.csproj src/MoveSync.Infrastructure/
COPY src/MoveSync.Api/MoveSync.Api.csproj src/MoveSync.Api/
COPY tests/MoveSync.UnitTests/MoveSync.UnitTests.csproj tests/MoveSync.UnitTests/
COPY tests/MoveSync.IntegrationTests/MoveSync.IntegrationTests.csproj tests/MoveSync.IntegrationTests/
RUN dotnet restore MoveSync.sln

COPY src/ src/
COPY tests/ tests/
RUN dotnet test tests/MoveSync.UnitTests/MoveSync.UnitTests.csproj -c Release --no-restore
RUN dotnet publish src/MoveSync.Api/MoveSync.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EXPOSE 8080
COPY --from=build /app/publish .
# Usuário sem privilégios de root (já existe nas imagens oficiais do .NET).
USER $APP_UID
ENTRYPOINT ["dotnet", "MoveSync.Api.dll"]
