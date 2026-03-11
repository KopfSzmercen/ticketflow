using Microsoft.Azure.Functions.Worker;

namespace TicketFlow.Functions.Activities;

public sealed class ProcessPaymentActivity
{
    [Function(nameof(ProcessPaymentActivity))]
    public Task<bool> RunActivity(
        [ActivityTrigger] Input input,
        FunctionContext executionContext)
    {
        // Simulate network latency / gateway call.
        return Task.FromResult(input.SimulatePaymentSuccess);
    }

    public sealed record Input(string OrderId, bool SimulatePaymentSuccess);
}
