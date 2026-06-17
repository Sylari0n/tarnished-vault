# The Tarnished Vault — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and deploy a distributed game loadout REST API on Azure satisfying all 6 mandatory assignment requirements by midnight 2026-06-17.

**Architecture:** C# .NET 9 Minimal API containerized with Docker, hosted on Azure App Service B1 Linux. Build/fashion data stored as JSON documents in Azure CosmosDB Serverless (partition key `/game`). Character screenshots uploaded to Azure Blob Storage and their public URL embedded in the CosmosDB document. A Blob-triggered Azure Function logs upload metadata asynchronously. GitHub Actions builds the Docker image, pushes to GitHub Container Registry (GHCR), and redeploys App Service on every push to `main`.

**Tech Stack:** C# .NET 9, Azure App Service B1 Linux, Azure CosmosDB Serverless, Azure Blob Storage Standard LRS, Azure Functions v4 .NET Isolated, GitHub Container Registry (GHCR), GitHub Actions, Azure CLI (IaC), Docker

## Global Constraints

- Target framework: `net9.0` throughout both projects
- Azure region: `westeurope`
- Resource group name: `tarnished-vault-rg`
- CosmosDB: database `TarnishedVaultDB`, container `Builds`, partition key `/game`
- Blob container name: `screenshots` with public blob access
- Function App storage: same storage account as blob storage (`tarnishedvaultstorage`)
- Valid games: `warframe`, `helldivers2`, `eldenring`
- Valid sections: `build`, `fashion`
- Screenshots only accepted in `fashion` section
- No frontend, no unit tests — manually tested with curl
- Single serializer throughout: System.Text.Json (custom CosmosDB serializer)
- App Service B1 (not F1 — F1 does not support Docker containers on Linux)

---

## File Map

```
cloudProject/
├── CLAUDE.md                                          ✅ done
├── src/
│   ├── TarnishedVault.Api/
│   │   ├── TarnishedVault.Api.csproj                  Task 1
│   │   ├── appsettings.json                           Task 1
│   │   ├── Models/
│   │   │   └── Build.cs                               Task 2
│   │   ├── Services/
│   │   │   └── CosmosSystemTextJsonSerializer.cs       Task 2
│   │   ├── Program.cs                                 Task 3
│   │   └── Dockerfile                                 Task 5
│   └── TarnishedVault.Functions/
│       ├── TarnishedVault.Functions.csproj            Task 4
│       ├── host.json                                  Task 4
│       ├── Program.cs                                 Task 4
│       └── BlobTriggerFunction.cs                     Task 4
├── infra/
│   └── provision.sh                                   Task 6
└── .github/
    └── workflows/
        └── deploy.yml                                 Task 7
```

---

### Task 1: Project Scaffolding

**Files:**
- Create: `src/TarnishedVault.Api/TarnishedVault.Api.csproj`
- Create: `src/TarnishedVault.Api/appsettings.json`
- Create: `.gitignore`

**Interfaces:**
- Produces: Two buildable .NET projects with all dependencies declared

- [ ] **Step 1: Create directory structure**

```bash
mkdir -p src/TarnishedVault.Api/Models
mkdir -p src/TarnishedVault.Api/Services
mkdir -p src/TarnishedVault.Functions
mkdir -p infra
mkdir -p .github/workflows
```

- [ ] **Step 2: Create the API project file**

Create `src/TarnishedVault.Api/TarnishedVault.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Cosmos" Version="3.42.0" />
    <PackageReference Include="Azure.Storage.Blobs" Version="12.22.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create appsettings.json**

Create `src/TarnishedVault.Api/appsettings.json`:

```json
{
  "CosmosDb": {
    "ConnectionString": "",
    "DatabaseName": "TarnishedVaultDB",
    "ContainerName": "Builds"
  },
  "BlobStorage": {
    "ConnectionString": "",
    "ContainerName": "screenshots"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 4: Create the Functions project file**

Create `src/TarnishedVault.Functions/TarnishedVault.Functions.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.0.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs" Version="6.6.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="2.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Create .gitignore**

Create `.gitignore` at project root:

```
bin/
obj/
*.user
.vs/
.vscode/
local.settings.json
appsettings.Development.json
*.pfx
.env
```

- [ ] **Step 6: Verify both projects restore without errors**

```bash
cd src/TarnishedVault.Api && dotnet restore
cd ../TarnishedVault.Functions && dotnet restore
```

Expected: `Restore completed` with no errors for both.

---

### Task 2: Data Model and CosmosDB Serializer

**Files:**
- Create: `src/TarnishedVault.Api/Models/Build.cs`
- Create: `src/TarnishedVault.Api/Services/CosmosSystemTextJsonSerializer.cs`

**Interfaces:**
- Produces: `Build` class used by Task 3 (Program.cs) and stored in CosmosDB
- Produces: `CosmosSystemTextJsonSerializer` registered in DI in Task 3

- [ ] **Step 1: Create Build.cs**

Create `src/TarnishedVault.Api/Models/Build.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TarnishedVault.Api.Models;

public class Build
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("game")]
    public string Game { get; set; } = string.Empty;

    [JsonPropertyName("section")]
    public string Section { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = "Anonymous";

    [JsonPropertyName("screenshotUrl")]
    public string? ScreenshotUrl { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    // Holds all game-specific fields (stats, weapons, etc.)
    // JsonElement preserves any JSON structure without type loss
    [JsonPropertyName("data")]
    public Dictionary<string, JsonElement> Data { get; set; } = new();
}
```

- [ ] **Step 2: Create CosmosSystemTextJsonSerializer.cs**

Create `src/TarnishedVault.Api/Services/CosmosSystemTextJsonSerializer.cs`:

```csharp
using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace TarnishedVault.Api.Services;

public class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    private readonly JsonSerializerOptions _options;

    public CosmosSystemTextJsonSerializer(JsonSerializerOptions options)
    {
        _options = options;
    }

    public override T FromStream<T>(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<T>(json, _options)
               ?? throw new InvalidOperationException("Deserialization returned null");
    }

    public override Stream ToStream<T>(T input)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(input, _options);
        return new MemoryStream(bytes);
    }
}
```

---

### Task 3: Core API — Program.cs

**Files:**
- Create: `src/TarnishedVault.Api/Program.cs`

**Interfaces:**
- Consumes: `Build` from `TarnishedVault.Api.Models`
- Consumes: `CosmosSystemTextJsonSerializer` from `TarnishedVault.Api.Services`
- Produces: Three HTTP endpoints: `POST /builds/{game}/{section}`, `GET /builds/{game}`, `GET /builds/{game}/{section}`

- [ ] **Step 1: Write Program.cs**

Create `src/TarnishedVault.Api/Program.cs`:

```csharp
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using TarnishedVault.Api.Models;
using TarnishedVault.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
};

// CosmosDB client with System.Text.Json serializer
var cosmosConnectionString = builder.Configuration["CosmosDb:ConnectionString"]
    ?? throw new InvalidOperationException("CosmosDb:ConnectionString is not configured");

builder.Services.AddSingleton(new CosmosClient(
    cosmosConnectionString,
    new CosmosClientOptions { Serializer = new CosmosSystemTextJsonSerializer(jsonOptions) }
));

// Blob Storage client
var blobConnectionString = builder.Configuration["BlobStorage:ConnectionString"]
    ?? throw new InvalidOperationException("BlobStorage:ConnectionString is not configured");

builder.Services.AddSingleton(new BlobServiceClient(blobConnectionString));

var app = builder.Build();

// Ensure CosmosDB container and Blob container exist on startup
var cosmosClient = app.Services.GetRequiredService<CosmosClient>();
var dbResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync("TarnishedVaultDB");
await dbResponse.Database.CreateContainerIfNotExistsAsync("Builds", "/game");

var blobServiceClient = app.Services.GetRequiredService<BlobServiceClient>();
var screenshotsContainer = blobServiceClient.GetBlobContainerClient("screenshots");
await screenshotsContainer.CreateIfNotExistsAsync(PublicAccessType.Blob);

// ── Valid values ──────────────────────────────────────────────────────────
string[] validGames    = ["warframe", "helldivers2", "eldenring"];
string[] validSections = ["build", "fashion"];

// ── POST /builds/{game}/{section} ─────────────────────────────────────────
app.MapPost("/builds/{game}/{section}", async (
    string game,
    string section,
    HttpRequest request,
    CosmosClient cosmos,
    BlobServiceClient blobClient) =>
{
    game    = game.ToLower();
    section = section.ToLower();

    if (!validGames.Contains(game))
        return Results.BadRequest($"Invalid game. Valid values: {string.Join(", ", validGames)}");

    if (!validSections.Contains(section))
        return Results.BadRequest($"Invalid section. Valid values: {string.Join(", ", validSections)}");

    var form = await request.ReadFormAsync();
    var dataJson = form["data"].ToString();

    if (string.IsNullOrWhiteSpace(dataJson))
        return Results.BadRequest("Missing 'data' form field. Send build data as a JSON string.");

    Dictionary<string, JsonElement> data;
    try
    {
        data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson, jsonOptions)
               ?? new Dictionary<string, JsonElement>();
    }
    catch
    {
        return Results.BadRequest("Invalid JSON in 'data' field.");
    }

    var author = data.TryGetValue("author", out var authorEl)
        ? authorEl.GetString() ?? "Anonymous"
        : "Anonymous";

    var build = new Build
    {
        Game      = game,
        Section   = section,
        Author    = author,
        Data      = data
    };

    // Upload screenshot only for fashion section
    if (section == "fashion" && form.Files.Count > 0)
    {
        var file     = form.Files[0];
        var blobName = $"{build.Id}-{Path.GetFileName(file.FileName)}";
        var blob     = blobClient.GetBlobContainerClient("screenshots").GetBlobClient(blobName);

        await blob.UploadAsync(
            file.OpenReadStream(),
            new BlobHttpHeaders { ContentType = file.ContentType });

        build.ScreenshotUrl = blob.Uri.ToString();
    }

    var container = cosmos.GetContainer("TarnishedVaultDB", "Builds");
    var response  = await container.CreateItemAsync(build, new PartitionKey(build.Game));

    return Results.Created($"/builds/{game}/{section}/{response.Resource.Id}", response.Resource);
});

// ── GET /builds/{game} ────────────────────────────────────────────────────
app.MapGet("/builds/{game}", async (string game, CosmosClient cosmos) =>
{
    var query = new QueryDefinition("SELECT * FROM c WHERE c.game = @game")
        .WithParameter("@game", game.ToLower());

    return Results.Ok(await FetchBuilds(cosmos, query));
});

// ── GET /builds/{game}/{section} ──────────────────────────────────────────
app.MapGet("/builds/{game}/{section}", async (string game, string section, CosmosClient cosmos) =>
{
    var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.game = @game AND c.section = @section")
        .WithParameter("@game", game.ToLower())
        .WithParameter("@section", section.ToLower());

    return Results.Ok(await FetchBuilds(cosmos, query));
});

app.Run();

// ── Helper ────────────────────────────────────────────────────────────────
static async Task<List<Build>> FetchBuilds(CosmosClient cosmos, QueryDefinition query)
{
    var container = cosmos.GetContainer("TarnishedVaultDB", "Builds");
    var iterator  = container.GetItemQueryIterator<Build>(query);
    var results   = new List<Build>();

    while (iterator.HasMoreResults)
    {
        var page = await iterator.ReadNextAsync();
        results.AddRange(page);
    }

    return results;
}
```

- [ ] **Step 2: Verify it builds**

```bash
cd src/TarnishedVault.Api
dotnet build
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s)`

---

### Task 4: Azure Function — Blob Trigger Logger

**Files:**
- Create: `src/TarnishedVault.Functions/Program.cs`
- Create: `src/TarnishedVault.Functions/BlobTriggerFunction.cs`
- Create: `src/TarnishedVault.Functions/host.json`

**Interfaces:**
- Consumes: `AzureWebJobsStorage` connection string (set in Azure Function App settings — same storage account as blob storage)
- Produces: Log entry per screenshot uploaded, visible in Azure Monitor / Function logs

- [ ] **Step 1: Create Functions host builder**

Create `src/TarnishedVault.Functions/Program.cs`:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .Build();

host.Run();
```

- [ ] **Step 2: Create BlobTriggerFunction**

Create `src/TarnishedVault.Functions/BlobTriggerFunction.cs`:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TarnishedVault.Functions;

public class BlobTriggerFunction
{
    private readonly ILogger<BlobTriggerFunction> _logger;

    public BlobTriggerFunction(ILogger<BlobTriggerFunction> logger)
    {
        _logger = logger;
    }

    [Function("ScreenshotUploadLogger")]
    public void Run(
        [BlobTrigger("screenshots/{name}", Connection = "AzureWebJobsStorage")] Stream stream,
        string name)
    {
        _logger.LogInformation(
            "[Tarnished Vault] Screenshot uploaded — Name: {Name}, Size: {Size} bytes, Timestamp: {Time}",
            name,
            stream.Length,
            DateTime.UtcNow);
    }
}
```

- [ ] **Step 3: Create host.json**

Create `src/TarnishedVault.Functions/host.json`:

```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true
      }
    }
  }
}
```

- [ ] **Step 4: Verify it builds**

```bash
cd src/TarnishedVault.Functions
dotnet build
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s)`

---

### Task 5: Dockerfile

**Files:**
- Create: `src/TarnishedVault.Api/Dockerfile`

**Interfaces:**
- Produces: Docker image `tarnished-vault-api` that runs the API on port 8080
- Consumed by: Task 7 (GitHub Actions) and App Service

- [ ] **Step 1: Write Dockerfile**

Create `src/TarnishedVault.Api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["TarnishedVault.Api.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TarnishedVault.Api.dll"]
```

- [ ] **Step 2: Verify Docker build succeeds locally**

```bash
cd src/TarnishedVault.Api
docker build -t tarnished-vault-api:local .
```

Expected: `Successfully built <image-id>` and `Successfully tagged tarnished-vault-api:local`

---

### Task 6: IaC Script — provision.sh

**Files:**
- Create: `infra/provision.sh`

**Interfaces:**
- Consumes: Active `az login` session (user must be logged in)
- Produces: All Azure resources created; outputs CosmosDB and Blob Storage connection strings to terminal

- [ ] **Step 1: Write provision.sh**

Create `infra/provision.sh`:

```bash
#!/bin/bash
set -e

# ── Variables ─────────────────────────────────────────────────────────────
RESOURCE_GROUP="tarnished-vault-rg"
LOCATION="westeurope"
APP_SERVICE_PLAN="tarnished-vault-plan"
APP_SERVICE="tarnished-vault-api"
COSMOS_ACCOUNT="tarnished-vault-cosmos"
STORAGE_ACCOUNT="tarnishedvaultstorage"
FUNCTION_APP="tarnished-vault-func"

echo "──────────────────────────────────────────────"
echo " The Tarnished Vault — Azure Provisioning"
echo "──────────────────────────────────────────────"

# ── Resource Group ────────────────────────────────────────────────────────
echo "[1/8] Creating Resource Group..."
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --output none
echo "      ✓ Resource Group ready"

# ── App Service Plan (B1 Linux — required for Docker containers) ──────────
echo "[2/8] Creating App Service Plan (B1 Linux)..."
az appservice plan create \
  --name "$APP_SERVICE_PLAN" \
  --resource-group "$RESOURCE_GROUP" \
  --sku B1 \
  --is-linux \
  --output none
echo "      ✓ App Service Plan ready"

# ── App Service (Docker container mode) ───────────────────────────────────
echo "[3/8] Creating App Service..."
az webapp create \
  --name "$APP_SERVICE" \
  --resource-group "$RESOURCE_GROUP" \
  --plan "$APP_SERVICE_PLAN" \
  --deployment-container-image-name "mcr.microsoft.com/dotnet/samples:aspnetapp" \
  --output none
echo "      ✓ App Service ready (using placeholder image — GitHub Actions will deploy the real one)"

# ── CosmosDB Account (Serverless — cheapest option) ───────────────────────
echo "[4/8] Creating CosmosDB account (this takes ~2 minutes)..."
az cosmosdb create \
  --name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --kind GlobalDocumentDB \
  --capabilities EnableServerless \
  --default-consistency-level Session \
  --output none
echo "      ✓ CosmosDB account ready"

# ── CosmosDB Database and Container ───────────────────────────────────────
echo "[5/8] Creating CosmosDB database and container..."
az cosmosdb sql database create \
  --account-name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --name "TarnishedVaultDB" \
  --output none

az cosmosdb sql container create \
  --account-name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --database-name "TarnishedVaultDB" \
  --name "Builds" \
  --partition-key-path "/game" \
  --output none
echo "      ✓ CosmosDB database and container ready"

# ── Storage Account ───────────────────────────────────────────────────────
echo "[6/8] Creating Storage Account..."
az storage account create \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --sku Standard_LRS \
  --output none

STORAGE_KEY=$(az storage account keys list \
  --account-name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[0].value" -o tsv)

# Create screenshots blob container with public blob access
az storage container create \
  --name "screenshots" \
  --account-name "$STORAGE_ACCOUNT" \
  --account-key "$STORAGE_KEY" \
  --public-access blob \
  --output none
echo "      ✓ Storage Account and screenshots container ready"

# ── Function App (Consumption plan — uses same Storage Account) ───────────
echo "[7/8] Creating Function App..."
az functionapp create \
  --name "$FUNCTION_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --storage-account "$STORAGE_ACCOUNT" \
  --consumption-plan-location "$LOCATION" \
  --runtime dotnet-isolated \
  --runtime-version 9 \
  --functions-version 4 \
  --output none
echo "      ✓ Function App ready"

# ── Service Principal for GitHub Actions ──────────────────────────────────
echo "[8/8] Creating Service Principal for GitHub Actions..."
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
SP_JSON=$(az ad sp create-for-rbac \
  --name "tarnished-vault-github-sp" \
  --role contributor \
  --scopes "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP" \
  --json-auth)
echo "      ✓ Service Principal created"

# ── Retrieve Connection Strings ───────────────────────────────────────────
echo ""
echo "──────────────────────────────────────────────"
echo " Retrieving connection strings..."
echo "──────────────────────────────────────────────"

COSMOS_CONNECTION=$(az cosmosdb keys list \
  --name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" -o tsv)

STORAGE_CONNECTION=$(az storage account show-connection-string \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --query "connectionString" -o tsv)

# ── Set App Service Environment Variables ────────────────────────────────
echo "Setting App Service configuration..."
az webapp config appsettings set \
  --name "$APP_SERVICE" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    "CosmosDb__ConnectionString=$COSMOS_CONNECTION" \
    "BlobStorage__ConnectionString=$STORAGE_CONNECTION" \
  --output none

# ── Set Function App Environment Variable ─────────────────────────────────
echo "Setting Function App configuration..."
az functionapp config appsettings set \
  --name "$FUNCTION_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    "AzureWebJobsStorage=$STORAGE_CONNECTION" \
  --output none

# ── Final Output ──────────────────────────────────────────────────────────
echo ""
echo "══════════════════════════════════════════════"
echo " ALL RESOURCES CREATED SUCCESSFULLY"
echo "══════════════════════════════════════════════"
echo ""
echo "App Service URL:"
echo "  https://$APP_SERVICE.azurewebsites.net"
echo ""
echo "COSMOS_CONNECTION_STRING:"
echo "  $COSMOS_CONNECTION"
echo ""
echo "BLOB_STORAGE_CONNECTION_STRING:"
echo "  $STORAGE_CONNECTION"
echo ""
echo "GITHUB_SECRET — AZURE_CREDENTIALS (copy everything between the lines):"
echo "────────────────────────────────────────────────────────────────────────"
echo "$SP_JSON"
echo "────────────────────────────────────────────────────────────────────────"
echo ""
echo "Next steps:"
echo "  1. Copy AZURE_CREDENTIALS above → GitHub repo → Settings → Secrets → AZURE_CREDENTIALS"
echo "  2. Create GitHub repo and push code"
echo "  3. GitHub Actions will build Docker image and deploy automatically"
```

- [ ] **Step 2: Make script executable**

```bash
chmod +x infra/provision.sh
```

---

### Task 7: GitHub Actions — CI/CD Pipeline

**Files:**
- Create: `.github/workflows/deploy.yml`

**Interfaces:**
- Consumes: GitHub secret `AZURE_CREDENTIALS` (service principal JSON from provision.sh output)
- Consumes: `GITHUB_TOKEN` (automatically provided by GitHub Actions)
- Produces: Docker image pushed to `ghcr.io/USERNAME/tarnished-vault-api:latest`; App Service updated to use the new image

- [ ] **Step 1: Write deploy.yml**

Create `.github/workflows/deploy.yml`:

```yaml
name: Build and Deploy

on:
  push:
    branches: [main]

env:
  IMAGE_NAME: tarnished-vault-api
  APP_SERVICE_NAME: tarnished-vault-api
  RESOURCE_GROUP: tarnished-vault-rg

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Log in to GitHub Container Registry
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push Docker image
        uses: docker/build-push-action@v5
        with:
          context: ./src/TarnishedVault.Api
          push: true
          tags: ghcr.io/${{ github.repository_owner }}/${{ env.IMAGE_NAME }}:latest

      - name: Log in to Azure
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Update App Service container image
        run: |
          az webapp config container set \
            --name ${{ env.APP_SERVICE_NAME }} \
            --resource-group ${{ env.RESOURCE_GROUP }} \
            --container-image-name ghcr.io/${{ github.repository_owner }}/${{ env.IMAGE_NAME }}:latest

      - name: Restart App Service
        run: |
          az webapp restart \
            --name ${{ env.APP_SERVICE_NAME }} \
            --resource-group ${{ env.RESOURCE_GROUP }}
```

---

### Task 8: Provision Azure Resources (Manual — User Action)

These steps require your terminal and Azure account. I will guide you through each one.

- [ ] **Step 1: Run the provision script**

```bash
cd /home/halish/Desktop/cloudProject
bash infra/provision.sh
```

Expected: Takes ~5 minutes. At the end you see connection strings and AZURE_CREDENTIALS JSON printed.

- [ ] **Step 2: Save the AZURE_CREDENTIALS output**

Copy the JSON block printed between the dashed lines. You will paste it into GitHub as a secret in Task 9.

---

### Task 9: GitHub Repo + Secrets (Manual — User Action)

- [ ] **Step 1: Create GitHub repository**

Go to [github.com/new](https://github.com/new), create a repo named `tarnished-vault`. Set it to **Public**. Do not add README or .gitignore (we have our own).

- [ ] **Step 2: Push code to GitHub**

```bash
cd /home/halish/Desktop/cloudProject
git init
git add .
git commit -m "feat: initial Tarnished Vault implementation"
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/tarnished-vault.git
git push -u origin main
```

Replace `YOUR_USERNAME` with your GitHub username.

- [ ] **Step 3: Add AZURE_CREDENTIALS secret**

In GitHub repo → Settings → Secrets and variables → Actions → New repository secret:
- Name: `AZURE_CREDENTIALS`
- Value: paste the JSON from provision.sh output

- [ ] **Step 4: Make GHCR package public (after first push builds)**

After the GitHub Actions workflow runs (check Actions tab — takes ~3 minutes):
- Go to your GitHub profile → Packages → `tarnished-vault-api`
- Package settings → Change visibility → Public → confirm

This allows Azure App Service to pull the image without authentication.

- [ ] **Step 5: Update App Service to use public GHCR image**

```bash
az webapp config container set \
  --name tarnished-vault-api \
  --resource-group tarnished-vault-rg \
  --container-image-name ghcr.io/YOUR_USERNAME/tarnished-vault-api:latest
az webapp restart --name tarnished-vault-api --resource-group tarnished-vault-rg
```

---

### Task 10: End-to-End Test

- [ ] **Step 1: Get the live URL**

```bash
az webapp show \
  --name tarnished-vault-api \
  --resource-group tarnished-vault-rg \
  --query "defaultHostName" -o tsv
```

Expected output: `tarnished-vault-api.azurewebsites.net`

- [ ] **Step 2: POST a Warframe build (no screenshot)**

```bash
curl -X POST \
  "https://tarnished-vault-api.azurewebsites.net/builds/warframe/build" \
  -F 'data={"author":"TarnishedWarrior","frame":"Saryn Prime","primary":"Kuva Bramma","secondary":"Tenet Cycron","melee":"Stropha","companion":"Smeeta Kavat","aura":"Corrosive Projection","helminth":"Roar","description":"Saryn spore spread for ESO"}'
```

Expected: `201 Created` with JSON body containing the saved document.

- [ ] **Step 3: POST an Elden Ring fashion entry (with screenshot)**

```bash
curl -X POST \
  "https://tarnished-vault-api.azurewebsites.net/builds/eldenring/fashion" \
  -F 'data={"author":"TarnishedOne","helmet":"White Mask","chest":"Raptor Black Feathers","gauntlets":"Crucible Gauntlets","greaves":"Radahn Greaves","description":"Pure drip build"}' \
  -F "screenshot=@/path/to/any-image.png"
```

Expected: `201 Created` with `screenshotUrl` field pointing to Azure Blob Storage URL.

- [ ] **Step 4: GET all Warframe builds**

```bash
curl "https://tarnished-vault-api.azurewebsites.net/builds/warframe"
```

Expected: JSON array containing the build posted in Step 2.

- [ ] **Step 5: Verify Azure Function triggered**

In Azure Portal:
- Go to Function App `tarnished-vault-func`
- Functions → ScreenshotUploadLogger → Monitor
- You should see a log entry: `[Tarnished Vault] Screenshot uploaded — Name: ..., Size: ... bytes`

---

## Self-Review Against Assignment Requirements

| Requirement | Covered by |
|-------------|-----------|
| GitHub as code repo, integrated with App Service | Task 7 (GitHub Actions → App Service deploy) |
| Infrastructure created automatically via script | Task 6 (provision.sh) |
| NoSQL CosmosDB database | Task 3 (Program.cs — CreateItemAsync, GetItemQueryIterator) |
| At least one Docker container | Task 5 (Dockerfile) + Task 7 (built and deployed) |
| At least one Azure Function (serverless) | Task 4 (BlobTriggerFunction) |
| At least one cloud storage service | Task 3 (BlobServiceClient — screenshots container) |

All 6 mandatory requirements are covered. No gaps.
