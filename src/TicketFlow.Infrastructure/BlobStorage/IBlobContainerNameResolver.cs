namespace TicketFlow.Infrastructure.BlobStorage;

public interface IBlobContainerNameResolver
{
    string Resolve(string containerAlias);
}
