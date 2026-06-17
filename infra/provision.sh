#!/bin/bash
set -e

# ── Configuration ────────────────────────────────────────────────────────────
RESOURCE_GROUP="tarnished-vault-rg"
LOCATION="spaincentral"
APP_PLAN="tarnished-vault-plan"
APP_NAME="tarnished-vault-api"
COSMOS_ACCOUNT="tarnished-vault-cosmos"
STORAGE_ACCOUNT="tarnishedvaultstorage"
FUNC_APP="tarnished-vault-func"
# ─────────────────────────────────────────────────────────────────────────────

echo "=== [1/9] Creating resource group ==="
az group create --name "$RESOURCE_GROUP" --location "$LOCATION"

echo "=== [2/9] Creating App Service Plan (B1 Linux) ==="
az appservice plan create \
  --name "$APP_PLAN" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --sku B1 \
  --is-linux

echo "=== [3/9] Creating App Service (placeholder image) ==="
az webapp create \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --plan "$APP_PLAN" \
  --container-image-name "mcr.microsoft.com/dotnet/samples:aspnetapp"

echo "=== [4/9] Creating CosmosDB account (serverless, NoSQL) ==="
az cosmosdb create \
  --name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --capabilities EnableServerless \
  --default-consistency-level Session \
  --locations regionName=spaincentral failoverPriority=0 isZoneRedundant=false

echo "=== [4b] Creating CosmosDB database and container ==="
az cosmosdb sql database create \
  --account-name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --name "TarnishedVaultDB"

az cosmosdb sql container create \
  --account-name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --database-name "TarnishedVaultDB" \
  --name "Builds" \
  --partition-key-path "/game"

echo "=== [5/9] Creating Storage Account ==="
az storage account create \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --sku Standard_LRS \
  --kind StorageV2 \
  --allow-blob-public-access true

echo "=== [5b] Creating screenshots blob container (public read) ==="
az storage container create \
  --name "screenshots" \
  --account-name "$STORAGE_ACCOUNT" \
  --public-access blob

echo "=== [6/9] Creating Function App (Consumption) ==="
az functionapp create \
  --name "$FUNC_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --consumption-plan-location "$LOCATION" \
  --runtime dotnet-isolated \
  --runtime-version 9 \
  --functions-version 4 \
  --storage-account "$STORAGE_ACCOUNT" \
  --os-type Linux

echo "=== [7/9] Fetching connection strings ==="
COSMOS_CONN=$(az cosmosdb keys list \
  --name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" -o tsv)

STORAGE_CONN=$(az storage account show-connection-string \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --query connectionString -o tsv)

echo "=== [8/9] Configuring App Service app settings ==="
az webapp config appsettings set \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    "CosmosDb__ConnectionString=$COSMOS_CONN" \
    "BlobStorage__ConnectionString=$STORAGE_CONN"

echo "=== [8b] Configuring Function App settings ==="
az functionapp config appsettings set \
  --name "$FUNC_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    "AzureWebJobsStorage=$STORAGE_CONN"

echo "=== [9/9] Creating service principal for GitHub Actions ==="
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
SP_JSON=$(az ad sp create-for-rbac \
  --name "tarnished-vault-github-sp" \
  --role contributor \
  --scopes "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP" \
  --sdk-auth)

echo ""
echo "════════════════════════════════════════════════════════════"
echo "  PROVISIONING COMPLETE"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "App Service URL:"
echo "  https://${APP_NAME}.azurewebsites.net"
echo ""
echo "─── AZURE_CREDENTIALS (add this as a GitHub secret) ───────"
echo "$SP_JSON"
echo "────────────────────────────────────────────────────────────"
echo ""
echo "─── CosmosDB connection string ─────────────────────────────"
echo "$COSMOS_CONN"
echo "────────────────────────────────────────────────────────────"
echo ""
echo "─── Storage connection string ──────────────────────────────"
echo "$STORAGE_CONN"
echo "────────────────────────────────────────────────────────────"
