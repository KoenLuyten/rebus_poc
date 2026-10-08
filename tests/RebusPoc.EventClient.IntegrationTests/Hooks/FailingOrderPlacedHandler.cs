using Rebus.Handlers;
using RebusPoc.Contracts;

namespace RebusPoc.EventClient.IntegrationTests.Hooks;

/// <summary>
/// A second <see cref="OrderPlaced"/> handler that Rebus runs after <c>OrderPlacedHandler</c>, for the same message.
/// For armed orders it throws, so the message fails after <c>CreateOrderCommand</c> has committed.
/// </summary>
public class FailingOrderPlacedHandler(TestProbe probe) : IHandleMessages<OrderPlaced>
{
    public Task Handle(OrderPlaced message)
    {
        if (!probe.FailOrderPlacedFor.ContainsKey(message.OrderId))
        {
            return Task.CompletedTask;
        }

        probe.FailedAttempts.AddOrUpdate(message.OrderId, 1, (_, attempts) => attempts + 1);
        throw new InvalidOperationException($"Simulated failure after OrderPlacedHandler for order {message.OrderId}");
    }
}
