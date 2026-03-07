@description('Base name used when composing all resource names.')
param appName string

@description('Short environment name, e.g. dev or prod.')
param environment string

@description('Azure region for the storage account.')
param location string

// uniqueString produces a deterministic 13-char hash seeded on the resource group ID,
// guaranteeing global uniqueness and satisfying the 3-24 alphanumeric-only requirement.
var storageAccountName = 'st${uniqueString(resourceGroup().id)}'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  tags: {
    app: appName
    environment: environment
  }
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

// Blob container used by the Flex Consumption plan to store deployment packages.
// The Function App authenticates to this container via its system-assigned managed identity.
resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storageAccount.name}/default/deploymentpackages'
  properties: {
    publicAccess: 'None'
  }
}

@description('Storage account name — used by the Function App to connect via managed identity.')
output accountName string = storageAccount.name

@description('Primary blob service endpoint URL for the storage account.')
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob
