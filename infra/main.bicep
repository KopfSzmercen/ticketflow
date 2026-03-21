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

@description('Retention period in days for monitoring data.')
@minValue(30)
@maxValue(730)
param monitoringRetentionInDays int

@description('Daily ingestion cap for Application Insights in GB.')
@minValue(1)
@maxValue(100)
param monitoringDailyCapGb int

@description('Telemetry profile for runtime logging behavior (minimal or balanced).')
@allowed([
  'minimal'
  'balanced'
])
param monitoringSamplingProfile string

@description('Initial adaptive sampling percentage for Application Insights.')
@minValue(1)
@maxValue(100)
param monitoringSamplingPercentage int

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

module monitoring './modules/monitoring.bicep' = {
  scope: resourceGroup
  params: {
    appName: appName
    environment: environment
    location: location
    retentionInDays: monitoringRetentionInDays
    dailyCapGb: monitoringDailyCapGb
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
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
    telemetryProfile: monitoringSamplingProfile
    telemetrySamplingPercentage: monitoringSamplingPercentage
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
