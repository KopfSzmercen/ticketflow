using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace TicketFlow.Integration.Tests;

/// <summary>
/// Minimal no-op <see cref="DurableTaskClient"/> used in integration tests
/// to satisfy the <c>CreateOrderFunction</c> and <c>GetOrderFunction</c>
/// parameters without requiring a live Durable host.
/// </summary>
internal sealed class NoOpDurableTaskClient() : DurableTaskClient("test")
{
    public override Task<string> ScheduleNewOrchestrationInstanceAsync(
        TaskName orchestratorName,
        object? input,
        StartOrchestrationOptions? options,
        CancellationToken cancellation)
        => Task.FromResult(options?.InstanceId ?? Guid.NewGuid().ToString());

    public override Task<OrchestrationMetadata?> GetInstanceAsync(
        string instanceId,
        bool getInputsAndOutputs,
        CancellationToken cancellation)
        => Task.FromResult<OrchestrationMetadata?>(null);

    public override Task<OrchestrationMetadata?> GetInstancesAsync(
        string instanceId,
        bool getInputsAndOutputs,
        CancellationToken cancellation)
        => Task.FromResult<OrchestrationMetadata?>(null);

    public override Task RaiseEventAsync(
        string instanceId,
        string eventName,
        object? eventPayload,
        CancellationToken cancellation)
        => Task.CompletedTask;

    public override Task TerminateInstanceAsync(
        string instanceId,
        TerminateInstanceOptions? options,
        CancellationToken cancellation)
        => Task.CompletedTask;

    public override Task SuspendInstanceAsync(
        string instanceId,
        string? reason,
        CancellationToken cancellation)
        => Task.CompletedTask;

    public override Task ResumeInstanceAsync(
        string instanceId,
        string? reason,
        CancellationToken cancellation)
        => Task.CompletedTask;

    public override Task<PurgeResult> PurgeInstanceAsync(
        string instanceId,
        PurgeInstanceOptions? options,
        CancellationToken cancellation)
        => Task.FromResult(new PurgeResult(0));

    public override Task<PurgeResult> PurgeAllInstancesAsync(
        PurgeInstancesFilter filter,
        PurgeInstanceOptions? options,
        CancellationToken cancellation)
        => Task.FromResult(new PurgeResult(0));

    public override AsyncPageable<OrchestrationMetadata> GetAllInstancesAsync(
        OrchestrationQuery? filter = null)
        => throw new NotSupportedException("Not needed for integration tests.");

    public override Task<OrchestrationMetadata> WaitForInstanceStartAsync(
        string instanceId,
        bool getInputsAndOutputs,
        CancellationToken cancellation)
        => throw new NotSupportedException("Not needed for integration tests.");

    public override Task<OrchestrationMetadata> WaitForInstanceCompletionAsync(
        string instanceId,
        bool getInputsAndOutputs,
        CancellationToken cancellation)
        => throw new NotSupportedException("Not needed for integration tests.");

    public override ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}

