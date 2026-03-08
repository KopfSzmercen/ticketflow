using Xunit;

namespace TicketFlow.Integration.Tests.Fixtures;

[CollectionDefinition("IntegrationTests")]
public sealed class IntegrationTestCollection : ICollectionFixture<CosmosDbContainerFixture>;
