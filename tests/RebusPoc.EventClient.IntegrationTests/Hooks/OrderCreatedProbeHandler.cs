using Rebus.Handlers;
using RebusPoc.EventClient.Orders.Events;

namespace RebusPoc.EventClient.IntegrationTests.Hooks;

/// <summary>Runs next to the app's own <c>OrderCreatedHandler</c> and tells the tests which domain events arrived.</summary>
public class OrderCreatedProbeHandler(TestProbe probe) : IHandleMessages<OrderCreated>
{
    public Task Handle(OrderCreated message)
    {
        probe.OrderCreatedHandled(message.OrderId);
        return Task.CompletedTask;
    }
}
