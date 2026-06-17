# The Tarnished Vault — Cloud Computing Mini Project

## Project Summary
A distributed game loadout REST API built on Microsoft Azure for a Cloud Computing university assignment (Politécnico Castelo Branco). Student: Muhammed Emin Haliş (20250821). Erasmus student, solo work.

## What the App Does
Users can POST and GET character loadouts for three games. Each game has two sections:
- **Build** — gameplay mechanics (weapons, stats, abilities) → stored as JSON in CosmosDB
- **Fashion** — cosmetic appearance → stored as JSON in CosmosDB + screenshot image in Azure Blob Storage

## The Three Games and Their Fields

### Warframe
**Build:** `frame`, `primary`, `secondary`, `melee`, `companion`, `aura`, `helminth`, `description`
**Fashion:** `frameSkin`, `syandana`, `attachments`, `colorPalette`, `screenshotUrl`

### Helldivers 2
**Build:** `armor`, `primary`, `secondary`, `grenade`, `stratagems` (array of 4), `booster`, `description`
**Fashion:** `helmet`, `armorSkin`, `cape`, `screenshotUrl`

### Elden Ring
**Build:** `class`, `weapon`, `offhand`, `talismans` (array of 2-4), `greatRune`, `spiritAsh`, `description`
**Fashion:** `helmet`, `chest`, `gauntlets`, `greaves`, `screenshotUrl`

## Azure Services Used
| Service | Purpose | Tier |
|---------|---------|------|
| Azure App Service | Hosts the .NET API Docker container | F1 Free |
| Azure CosmosDB | NoSQL JSON document storage for all build/fashion data | Serverless |
| Azure Blob Storage | Stores screenshot images, returns public URL | Standard LRS |
| Azure Functions | Blob-triggered background logger (filename, size, timestamp) | Consumption |
| GitHub Actions | CI/CD — auto-deploys Docker container to App Service on push | Free |

## Tech Stack
- **Language:** C# .NET 9 Minimal API
- **Container:** Docker
- **IaC:** Bash script using Azure CLI (`infra/provision.sh`)
- **CI/CD:** GitHub Actions (`.github/workflows/deploy.yml`)

## Project Structure
```
TarnishedVault/
├── src/
│   ├── TarnishedVault.Api/
│   │   ├── Program.cs               # entry point + all endpoints
│   │   ├── Models/Build.cs          # C# data model
│   │   ├── Services/CosmosDbService.cs
│   │   ├── Services/BlobService.cs
│   │   └── Dockerfile
│   └── TarnishedVault.Functions/
│       └── BlobTriggerFunction.cs
├── infra/
│   └── provision.sh                 # provisions all Azure resources
└── .github/workflows/
    └── deploy.yml
```

## CosmosDB Structure
- Database: `TarnishedVaultDB`
- Container: `Builds`
- Partition key: `/game`
- Each document has: `id`, `game`, `section` (build|fashion), `author`, game-specific fields, optional `screenshotUrl`

## Blob Storage Structure
- Container: `screenshots`
- Access: public read (so URLs work directly in API responses)

## Assignment Requirements Status
- [x] GitHub + Azure App Service integration (CI/CD)
- [x] IaC script (provision.sh with Azure CLI)
- [x] CosmosDB NoSQL
- [x] Docker container
- [x] Azure Function (blob trigger)
- [x] Blob Storage

## Key Decisions
- No frontend — REST API only, tested with curl/Postman (not required by assignment)
- Minimal API pattern — flat file structure, no unnecessary abstractions
- All endpoints in Program.cs to keep code minimal and readable
- Screenshot only exists in Fashion section (not Build) — keeps Blob usage purposeful

## Azure Resource Names (set during provision.sh)
- Resource Group: `tarnished-vault-rg`
- App Service Plan: `tarnished-vault-plan`
- App Service: `tarnished-vault-api`
- CosmosDB Account: `tarnished-vault-cosmos`
- Storage Account: `tarnishedvaultstorage`
- Function App: `tarnished-vault-func`
- Location: `westeurope`
