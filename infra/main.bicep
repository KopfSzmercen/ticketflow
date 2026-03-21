targetScope = 'subscription'

@description('Short name for the environment, e.g. dev or prod.')
param environment string

@description('Azure region for all resources.')
param location string

@description('Base name used when composing all resource names.')
param appName string

@description('Service Bus topic for order lifecycle events.')
param serviceBusOrderEventsTopicName string

@description('Subscription name for email worker.')
param serviceBusEmailWorkerSubscriptionName string

@description('Subscription name for analytics worker.')
param serviceBusAnalyticsWorkerSubscriptionName string

@description('Subscription name for QR worker.')
param serviceBusQrWorkerSubscriptionName string

var resourceGroupName = 'rg-${appName}-${environment}'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
}

module storage './modules/storage.bicep' = {
  scope: resourceGroup
  params: {
    appName: appName
    environment: environment
    location: location
  }
}

module cosmos './modules/cosmos.bicep' = {
  scope: resourceGroup
  params: {
    appName: appName
    environment: environment
    location: location
  }
}

module serviceBus './modules/serviceBus.bicep' = {
  scope: resourceGroup
  params: {
    appName: appName
    environment: environment
    location: location
    orderEventsTopicName: serviceBusOrderEventsTopicName
    emailWorkerSubscriptionName: serviceBusEmailWorkerSubscriptionName
    analyticsWorkerSubscriptionName: serviceBusAnalyticsWorkerSubscriptionName
    qrWorkerSubscriptionName: serviceBusQrWorkerSubscriptionName
  }
}

module functions './modules/functions.bicep' = {
  scope: resourceGroup
  params: {
    appName: appName
    environment: environment
    location: location
    storageAccountName: storage.outputs.accountName
    storageAccountBlobEndpoint: storage.outputs.blobEndpoint
    cosmosAccountEndpoint: cosmos.outputs.accountEndpoint
    serviceBusNamespaceFqdn: serviceBus.outputs.namespaceFqdn
    serviceBusOrderEventsTopicName: serviceBus.outputs.topicName
    serviceBusEmailWorkerSubscriptionName: serviceBus.outputs.emailSubscriptionName
    serviceBusAnalyticsWorkerSubscriptionName: serviceBus.outputs.analyticsSubscriptionName
    serviceBusQrWorkerSubscriptionName: serviceBus.outputs.qrSubscriptionName
  }
}

// Grant the Function App's managed identity the roles it needs on Storage and Cosmos DB.
// This module runs after functions is deployed so the principalId is available.
module roleAssignments './modules/roleAssignments.bicep' = {
  scope: resourceGroup
  params: {
    storageAccountName: storage.outputs.accountName
    cosmosAccountName: cosmos.outputs.accountName
    serviceBusNamespaceName: serviceBus.outputs.namespaceName
    functionAppPrincipalId: functions.outputs.principalId
  }
}
