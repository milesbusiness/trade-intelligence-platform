# Deployment Guide

## Azure Infrastructure (Bicep)

Provision everything with one command:

```bash
# Create resource group
az group create --name rg-trade-intelligence --location westeurope

# Deploy all Azure resources
az deployment group create \
  --resource-group rg-trade-intelligence \
  --template-file infra/main.bicep \
  --parameters environment=prod
```

This provisions:
- Azure Container Apps (API)
- Azure AI Search Standard S1
- Azure OpenAI (GPT-4o + text-embedding-3-large)
- Azure Storage Account (GRS)
- Azure Key Vault

## Container Deployment

```bash
# Build and push
docker build -t ghcr.io/milesbusiness/trade-intelligence-platform:latest .
docker push ghcr.io/milesbusiness/trade-intelligence-platform:latest

# Deploy to Azure Container Apps
az containerapp update \
  --name tip-prod-api \
  --resource-group rg-trade-intelligence \
  --image ghcr.io/milesbusiness/trade-intelligence-platform:latest
```

## Environment Variables (Production)

Set via Azure Key Vault references in Container Apps:

| Variable | Source |
|----------|--------|
| `AzureSearch__Endpoint` | Key Vault secret |
| `AzureSearch__ApiKey` | Key Vault secret |
| `AzureOpenAI__Endpoint` | Key Vault secret |
| `AzureOpenAI__ApiKey` | Key Vault secret |
| `AzureStorage__ConnectionString` | Key Vault secret |

## Health Check

```bash
curl https://your-app.azurecontainerapps.io/health
# {"status":"healthy"}
```

## Scaling

Container Apps scales automatically (0–10 replicas) based on HTTP traffic. Minimum replicas set to 2 in production for availability.
