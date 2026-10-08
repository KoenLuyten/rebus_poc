using System.Collections.Concurrent;

namespace RebusPoc.EventClient.IntegrationTests;

/// <summary>What the order and outbox looked like inside the transaction, just before the test made it fail.</summary>
public record InTransactionSnapshot(int Orders, int OutboxMessages);

/// <summary>
/// Shared between the tests and the hooks registered in the app: the tests arm failures for a given order Id, and the
/// hooks report back which domain events were handled and what they saw inside the transaction.
/// </summary>
public class TestProbe
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _orderCreatedHandled = new();

    /// <summary>Orders for which <see cref="Hooks.FailingCommitInterceptor"/> throws when EF commits.</summary>
    public ConcurrentDictionary<Guid, bool> FailCommitFor { get; } = new();

    /// <summary>Orders for which <see cref="Hooks.FailingOrderPlacedHandler"/> throws after <c>OrderPlacedHandler</c> ran.</summary>
    public ConcurrentDictionary<Guid, bool> FailOrderPlacedFor { get; } = new();

    public ConcurrentDictionary<Guid, InTransactionSnapshot> Snapshots { get; } = new();

    public ConcurrentDictionary<Guid, int> FailedAttempts { get; } = new();

    public ConcurrentDictionary<Guid, int> OrderCreatedCount { get; } = new();

    public void OrderCreatedHandled(Guid orderId)
    {
        OrderCreatedCount.AddOrUpdate(orderId, 1, (_, count) => count + 1);
        Handled(orderId).TrySetResult();
    }

    public Task WaitForOrderCreated(Guid orderId) => Handled(orderId).Task.WaitAsync(Wait.Delivery);

    public bool WasOrderCreatedHandled(Guid orderId) => Handled(orderId).Task.IsCompleted;

    private TaskCompletionSource Handled(Guid orderId) =>
        _orderCreatedHandled.GetOrAdd(orderId, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
}
