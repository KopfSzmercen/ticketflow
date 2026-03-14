using Xunit;

namespace TicketFlow.Integration.Tests.Fixtures;

[CollectionDefinition("DurableIntegrationTests")]
public sealed class DurableIntegrationTestCollection : ICollectionFixture<DurableFunctionsHostFixture>;