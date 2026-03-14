using Microsoft.Azure.Functions.Worker;

namespace TicketFlow.Functions.Activities;

public sealed class CheckPaymentStatusActivity
{
    [Function(nameof(CheckPaymentStatusActivity))]
    public Task<bool> RunActivity(
        [ActivityTrigger] Input input,
        FunctionContext executionContext
    )
    {
        // In a real implementation, this would call the payment gateway API to check the payment status.
        // For demo purposes, we just return the simulated payment result from the input.
        return Task.FromResult(input.SimulatePaymentSuccess);
    }

    public sealed record Input(string OrderId, bool SimulatePaymentSuccess);
}
