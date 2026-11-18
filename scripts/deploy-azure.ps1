# =============================================================================
#  MoveSync API — publicação no Azure App Service (Linux) com Azure CLI — Windows/PowerShell
#
#  Pré-requisitos: Azure CLI logado (az login) e .NET SDK 10.
#  Uso (na raiz do repositório):
#
#    $env:ORACLE_CONN = "User Id=RMxxxxx;Password=<senha>;Data Source=oracle.fiap.com.br:1521/ORCL"
#    .\scripts\deploy-azure.ps1
#
#    # Sem Oracle (apenas para demonstração): SQLite em disco persistente do App Service
#    .\scripts\deploy-azure.ps1 -Banco sqlite
# =============================================================================
param(
    [string]$Rg = "rg-movesync",
    [string]$Local = "brazilsouth",
    [string]$Plano = "plan-movesync",
    [string]$App = "movesync-api-$(Get-Random -Maximum 99999)",
    [string]$Sku = "B1",
    [ValidateSet("oracle", "sqlite")][string]$Banco = "oracle"
)
$ErrorActionPreference = "Stop"

# Usuários de demonstração: os mesmos valores usados nos casos de teste de QA e no README.
$senhaGestor = if ($env:SENHA_GESTOR) { $env:SENHA_GESTOR } else { "Gestor@2026" }
$senhaCidada = if ($env:SENHA_CIDADA) { $env:SENHA_CIDADA } else { "Cidada@2026" }
$segredoSensores = if ($env:SEGREDO_SENSORES) { $env:SEGREDO_SENSORES } else { "sensor-demo-local-2026" }
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$jwtChave = [Convert]::ToBase64String($bytes)

if ($Banco -eq "oracle") {
    if (-not $env:ORACLE_CONN) { throw "Defina `$env:ORACLE_CONN com a connection string do Oracle (ou use -Banco sqlite)." }
    $provedor = "Oracle"; $conexao = $env:ORACLE_CONN
} else {
    $provedor = "Sqlite"; $conexao = "Data Source=/home/movesync.db"
}

Write-Host ">> 1/5 Grupo de recursos e plano"
az group create --name $Rg --location $Local --output none
az appservice plan create --resource-group $Rg --name $Plano --is-linux --sku $Sku --output none

Write-Host ">> 2/5 Web App (.NET 10)"
az webapp create --resource-group $Rg --plan $Plano --name $App --runtime "DOTNETCORE:10.0" --output none
az webapp config set --resource-group $Rg --name $App --always-on true --output none

Write-Host ">> 3/5 Configuração (variáveis de ambiente)"
az webapp config appsettings set --resource-group $Rg --name $App --output none --settings `
    "ASPNETCORE_ENVIRONMENT=Production" `
    "BancoDados__Provedor=$provedor" `
    "BancoDados__CriarTabelasAutomaticamente=true" `
    "ConnectionStrings__MoveSync=$conexao" `
    "Jwt__Chave=$jwtChave" `
    "Seed__Habilitado=true" `
    "Seed__GestorSenha=$senhaGestor" `
    "Seed__CidadaoSenha=$senhaCidada" `
    "Seed__SegredoSensores=$segredoSensores"

Write-Host ">> 4/5 Publicação"
if (Test-Path .\publish) { Remove-Item .\publish -Recurse -Force }
if (Test-Path .\movesync-api.zip) { Remove-Item .\movesync-api.zip -Force }
dotnet publish src/MoveSync.Api/MoveSync.Api.csproj -c Release -o .\publish
Compress-Archive -Path .\publish\* -DestinationPath .\movesync-api.zip
az webapp deploy --resource-group $Rg --name $App --src-path .\movesync-api.zip --type zip --output none

Write-Host ">> 5/5 Verificação"
$url = "https://" + (az webapp show --resource-group $Rg --name $App --query defaultHostName -o tsv)
Start-Sleep -Seconds 20
try { Invoke-RestMethod "$url/health" | ConvertTo-Json -Depth 5 } catch { Write-Host "Health ainda subindo; tente de novo em 1 minuto." }
Write-Host ""
Write-Host "API publicada:  $url/swagger"
Write-Host "Health check:   $url/health"
Write-Host "Guarde a URL: ela vai no README, no .txt de entrega e no lugar de [URL da API] nos casos de teste."
