@description('Base name used when composing all resource names.')
param appName string

@description('Short environment name, e.g. dev or prod.')
param environment string

@description('Azure region for Service Bus resources.')
param location string

@description('Service Bus topic for order lifecycle events.')
param orderEventsTopicName string

@description('Subscription name for email worker.')
param emailWorkerSubscriptionName string

@description('Subscription name for analytics worker.')
param analyticsWorkerSubscriptionName string

@description('Subscription name for QR worker.')
param qrWorkerSubscriptionName string

var namespaceName = 'sb-${appName}-${environment}'

resource namespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: namespaceName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    minimumTlsVersion: '1.2'
  }
}

resource orderEventsTopic 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: namespace
  name: orderEventsTopicName
  properties: {
    enableBatchedOperations: true
  }
}

resource emailWorkerSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: orderEventsTopic
  name: emailWorkerSubscriptionName
  properties: {
    maxDeliveryCount: 10
  }
}

resource analyticsWorkerSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: orderEventsTopic
  name: analyticsWorkerSubscriptionName
  properties: {
    maxDeliveryCount: 10
  }
}

resource qrWorkerSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: orderEventsTopic
  name: qrWorkerSubscriptionName
  properties: {
    maxDeliveryCount: 10
  }
}

output namespaceName string = namespace.name
output namespaceFqdn string = '${namespace.name}.servicebus.windows.net'
output topicName string = orderEventsTopic.name
output emailSubscriptionName string = emailWorkerSubscription.name
output analyticsSubscriptionName string = analyticsWorkerSubscription.name
output qrSubscriptionName string = qrWorkerSubscription.name
