using RebusPoc.Contracts;

namespace RebusPoc.EventClient.IntegrationTests;

/// <summary>The real <c>OrderPlaced</c> → <c>CreateOrderCommand</c> → <c>OrderCreated</c> flow.</summary>
[Collection(AppCollection.Name)]
public class OrderPlacedTests(AppFixture app)
{
    // Rebus's default number of delivery attempts before a message goes to the error queue
    private const int DeliveryAttempts = 5;

    [Fact]
    public async Task OrderPlaced_creates_the_order_and_publishes_OrderCreated()
    {
        var orderId = Guid.NewGuid();

        await app.SendToClient(new OrderPlaced(orderId, 20, DateTimeOffset.UtcNow));

        await app.Probe.WaitForOrderCreated(orderId);
        Assert.Equal(1, await OrdersDatabase.CountOrders(app.OrdersConnectionString, orderId));
        Assert.Equal(1, await OrdersDatabase.CountOutboxMessages(app.OrdersConnectionString, orderId));
    }

    // The command is the unit of work: once it committed, a later failure of the same Rebus message doesn't undo it.
    // Rebus retries the message, and the idempotent CreateOrderCommand neither duplicates the order nor the event.
    [Fact]
    public async Task Failure_after_the_command_committed_keeps_the_order_and_its_event_exactly_once()
    {
        var orderId = Guid.NewGuid();
        app.Probe.FailOrderPlacedFor[orderId] = true;

        await app.SendToClient(new OrderPlaced(orderId, 20, DateTimeOffset.UtcNow));
        await Wait.Until(async () => await OrdersDatabase.CountErrorQueueMessages(app.RebusConnectionString, orderId) == 1,
            "the message is in the error queue");

        Assert.Equal(DeliveryAttempts, app.Probe.FailedAttempts[orderId]);
        Assert.Equal(1, await OrdersDatabase.CountOrders(app.OrdersConnectionString, orderId));
        Assert.Equal(1, await OrdersDatabase.CountOutboxMessages(app.OrdersConnectionString, orderId));
        await app.Probe.WaitForOrderCreated(orderId);
        await Task.Delay(Wait.QuietPeriod, TestContext.Current.CancellationToken);
        Assert.Equal(1, app.Probe.OrderCreatedCount[orderId]);
    }
}
