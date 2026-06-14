@description('Environment name (dev, staging, prod)')
param environment string = 'dev'

@description('Azure region')
param location string = resourceGroup().location

var prefix = 'tip-${environment}'

// Azure AI Search
resource search 'Microsoft.Search/searchServices@2023-11-01' = {
  name: '${prefix}-search'
  location: location
  sku: { name: 'standard' }
  properties: {
    replicaCount: environment == 'prod' ? 2 : 1
    partitionCount: 1
    hostingMode: 'default'
    semanticSearch: 'standard'
  }
}

// Azure OpenAI
resource openai 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: '${prefix}-openai'
  location: location
  kind: 'OpenAI'
  sku: { name: 'S0' }
  properties: {
    customSubDomainName: '${prefix}-openai'
    publicNetworkAccess: 'Enabled'
  }
}

resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = {
  parent: openai
  name: 'gpt-4o'
  properties: {
    model: { format: 'OpenAI', name: 'gpt-4o', version: '2024-11-20' }
    raiPolicyName: 'Microsoft.Default'
  }
  sku: { name: 'Standard', capacity: 30 }
}

resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = {
  parent: openai
  dependsOn: [gpt4oDeployment]
  name: 'text-embedding-3-large'
  properties: {
    model: { format: 'OpenAI', name: 'text-embedding-3-large', version: '1' }
    raiPolicyName: 'Microsoft.Default'
  }
  sku: { name: 'Standard', capacity: 120 }
}

// Storage Account
resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: '${replace(prefix, '-', '')}stor'
  location: location
  sku: { name: environment == 'prod' ? 'Standard_GRS' : 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
  }
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${prefix}-kv'
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: environment == 'prod' ? true : false
  }
}

// Container Apps Environment
resource caEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: '${prefix}-cae'
  location: location
  properties: {
    zoneRedundant: environment == 'prod'
  }
}

// Container App
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${prefix}-api'
  location: location
  properties: {
    managedEnvironmentId: caEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      secrets: [
        { name: 'search-key', value: search.listAdminKeys().primaryKey }
        { name: 'openai-key', value: openai.listKeys().key1 }
        { name: 'storage-conn', value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=core.windows.net' }
      ]
    }
    template: {
      containers: [{
        name: 'trade-intelligence-api'
        image: 'ghcr.io/milesbusiness/trade-intelligence-platform:latest'
        resources: { cpu: json('0.5'), memory: '1Gi' }
        env: [
          { name: 'AzureSearch__Endpoint', value: 'https://${search.name}.search.windows.net' }
          { name: 'AzureSearch__ApiKey', secretRef: 'search-key' }
          { name: 'AzureSearch__IndexName', value: 'trade-documents' }
          { name: 'AzureOpenAI__Endpoint', value: openai.properties.endpoint }
          { name: 'AzureOpenAI__ApiKey', secretRef: 'openai-key' }
          { name: 'AzureStorage__ConnectionString', secretRef: 'storage-conn' }
        ]
      }]
      scale: {
        minReplicas: environment == 'prod' ? 2 : 0
        maxReplicas: 10
      }
    }
  }
}

output apiUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output searchEndpoint string = 'https://${search.name}.search.windows.net'
output openaiEndpoint string = openai.properties.endpoint
