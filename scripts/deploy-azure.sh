#!/usr/bin/env bash
# =============================================================================
#  MoveSync API — publicação no Azure App Service (Linux) com Azure CLI
#
#  Pré-requisitos: Azure CLI logado (az login), .NET SDK 10 e zip.
#  Uso (na raiz do repositório):
#
#    # Banco Oracle (FIAP ou outro Oracle acessível pela internet):
#    export ORACLE_CONN="User Id=RMxxxxx;Password=<senha>;Data Source=oracle.fiap.com.br:1521/ORCL"
#    bash scripts/deploy-azure.sh
#
#    # Sem Oracle (apenas para demonstração): SQLite em disco persistente do App Service
#    BANCO=sqlite bash scripts/deploy-azure.sh
#
#  Nenhum segredo fica no repositório: a chave JWT é gerada aqui e tudo vai para as
#  "App Settings" do App Service (viram variáveis de ambiente na aplicação).
# =============================================================================
set -euo pipefail

RG="${RG:-rg-movesync}"
LOCAL="${LOCAL:-brazilsouth}"
PLANO="${PLANO:-plan-movesync}"
APP="${APP:-movesync-api-$RANDOM}"          # precisa ser único no Azure inteiro
SKU="${SKU:-B1}"                            # B1 permite Always On (o worker de recálculo fica rodando)
BANCO="${BANCO:-oracle}"

# Usuários de demonstração: os mesmos valores usados nos casos de teste de QA e no README.
SENHA_GESTOR="${SENHA_GESTOR:-Gestor@2026}"
SENHA_CIDADA="${SENHA_CIDADA:-Cidada@2026}"
SEGREDO_SENSORES="${SEGREDO_SENSORES:-sensor-demo-local-2026}"
JWT_CHAVE="${JWT_CHAVE:-$(openssl rand -base64 48)}"

if [[ "$BANCO" == "oracle" ]]; then
  : "${ORACLE_CONN:?Defina ORACLE_CONN com a connection string do Oracle (ou use BANCO=sqlite)}"
  PROVEDOR="Oracle"
  CONEXAO="$ORACLE_CONN"
else
  PROVEDOR="Sqlite"
  CONEXAO="Data Source=/home/movesync.db"   # /home é persistente no App Service Linux
fi

echo ">> 1/5 Grupo de recursos e plano"
az group create --name "$RG" --location "$LOCAL" --output none
az appservice plan create --resource-group "$RG" --name "$PLANO" --is-linux --sku "$SKU" --output none

echo ">> 2/5 Web App (.NET 10)"
# Se o runtime não aparecer em 'az webapp list-runtimes --os linux', publiquem pelo dockerfiles/Dockerfile.
az webapp create --resource-group "$RG" --plan "$PLANO" --name "$APP" --runtime "DOTNETCORE:10.0" --output none
az webapp config set --resource-group "$RG" --name "$APP" --always-on true --output none

echo ">> 3/5 Configuração (variáveis de ambiente)"
az webapp config appsettings set --resource-group "$RG" --name "$APP" --output none --settings \
  ASPNETCORE_ENVIRONMENT=Production \
  BancoDados__Provedor="$PROVEDOR" \
  BancoDados__CriarTabelasAutomaticamente=true \
  ConnectionStrings__MoveSync="$CONEXAO" \
  Jwt__Chave="$JWT_CHAVE" \
  Seed__Habilitado=true \
  Seed__GestorSenha="$SENHA_GESTOR" \
  Seed__CidadaoSenha="$SENHA_CIDADA" \
  Seed__SegredoSensores="$SEGREDO_SENSORES"

echo ">> 4/5 Publicação"
rm -rf ./publish ./movesync-api.zip
dotnet publish src/MoveSync.Api/MoveSync.Api.csproj -c Release -o ./publish
(cd ./publish && zip -qr ../movesync-api.zip .)
az webapp deploy --resource-group "$RG" --name "$APP" --src-path ./movesync-api.zip --type zip --output none

echo ">> 5/5 Verificação"
URL="https://$(az webapp show --resource-group "$RG" --name "$APP" --query defaultHostName -o tsv)"
sleep 20
curl -s "$URL/health" || true
echo
echo "API publicada:  $URL/swagger"
echo "Health check:   $URL/health"
echo "Guarde a URL: ela vai no README, no .txt de entrega e no lugar de [URL da API] nos casos de teste."
