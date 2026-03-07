@description('Name of the Storage Account to grant access to.')
param storageAccountName string

@description('Name of the Cosmos DB account to grant access to.')
param cosmosAccountName string

@description('Principal ID of the Function App system-assigned managed identity.')
param functionAppPrincipalId string

// The three Storage roles required by the Azure Functions runtime when connecting
// to AzureWebJobsStorage via managed identity (no connection string).
// https://learn.microsoft.com/azure/azure-functions/functions-reference#connecting-to-host-storage-with-an-identity
var storageRoleIds = [
  'b7e6dc6d-f1e8-4753-8033-0f276bb0955b' // Storage Blob Data Owner
  '974c5e8b-45b9-4653-ba55-5f855dd0fb88' // Storage Queue Data Contributor
  '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3' // Storage Table Data Contributor
]

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource storageRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for roleId in storageRoleIds: {
    // guid() is deterministic: same inputs always produce the same GUID, so re-deployments are idempotent.
    name: guid(storageAccount.id, functionAppPrincipalId, roleId)
    scope: storageAccount
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleId)
      principalId: functionAppPrincipalId
      principalType: 'ServicePrincipal'
    }
  }
]

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' existing = {
  name: cosmosAccountName
}

// Cosmos DB Built-in Data Contributor (id: 00000000-0000-0000-0000-000000000002) is a
// data-plane role that allows reads and writes to items — it is NOT an ARM role.
resource cosmosRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, functionAppPrincipalId, '00000000-0000-0000-0000-000000000002')
  properties: {
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    principalId: functionAppPrincipalId
    scope: cosmosAccount.id
  }
}
