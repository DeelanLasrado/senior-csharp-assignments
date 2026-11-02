#!/usr/bin/env bash
# Redeploy when the Azure subscription is writable again (currently ReadOnlyDisabled).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
RG="${AZ_RG:-rg-senior-csharp-assignments}"
PLAN="${AZ_PLAN:-plan-senior-csharp}"
APP="${AZ_APP_NAME:-senior-csharp-demo-deelan}"
LOC="${AZ_LOCATION:-eastus}"

az group create --name "$RG" --location "$LOC"
az appservice plan create --name "$PLAN" --resource-group "$RG" --sku FREE --is-linux
az webapp create --name "$APP" --resource-group "$RG" --plan "$PLAN" --runtime "DOTNETCORE:8.0" || true

dotnet publish "$ROOT/src/SeniorDevAssignments.Web/SeniorDevAssignments.Web.csproj" -c Release -o "$ROOT/artifacts/web"
cd "$ROOT/artifacts/web"
zip -r ../web.zip .
az webapp deploy --resource-group "$RG" --name "$APP" --src-path "$ROOT/artifacts/web.zip" --type zip
echo "Live at: https://${APP}.azurewebsites.net"
