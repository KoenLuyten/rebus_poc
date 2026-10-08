using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RebusPoc.EventClient.IntegrationTests.Hooks;
using RebusPoc.EventClient.Orders.Commands;

namespace RebusPoc.EventClient.IntegrationTests;

/// <summary>How the test sends <see cref="CreateOrderCommand"/>.</summary>
public enum Sender
{
    /// <summary>Straight through MediatR, like the JSON-RPC endpoint.</summary>
    Mediator,

    /// <summary>Through MediatR from inside a Rebus handler, like <c>OrderPlacedHandler</c>.</summary>
    RebusHandler,
}

/// <summary>
/// Checks that <c>CreateOrderCommand</c>'s order (EF) and its <c>OrderCreated</c> event (Rebus outbox) are committed in
/// one transaction: both or neither, also when the failure comes after the outbox write.
/// </summary>
[Collection(AppCollection.Name)]
public class OutboxTransactionTests(AppFixture app)
{
    [Theory]
    [InlineData(Sender.Mediator)]
    [InlineData(Sender.RebusHandler)]
    public async Task Successful_command_commits_the_order_and_its_outbox_message_together(Sender sender)
    {
        var orderId = Guid.NewGuid();

        await CreateOrder(sender, orderId);

        await app.Probe.WaitForOrderCreated(orderId);
        Assert.Equal(1, await OrdersDatabase.CountOrders(app.OrdersConnectionString, orderId));
        Assert.Equal(1, await OrdersDatabase.CountOutboxMessages(app.OrdersConnectionString, orderId));
    }

    [Theory]
    [InlineData(Sender.Mediator)]
    [InlineData(Sender.RebusHandler)]
    public async Task Handler_failure_after_sending_the_event_rolls_back_the_order_and_its_outbox_message_together(Sender sender)
    {
        var orderId = Guid.NewGuid();

        await CreateOrder(sender, orderId, simulateFailure: true, expectFailure: true);

        await AssertNothingCommitted(orderId);
    }

    [Theory]
    [InlineData(Sender.Mediator)]
    [InlineData(Sender.RebusHandler)]
    public async Task Commit_failure_after_the_outbox_write_rolls_back_the_order_and_its_outbox_message_together(Sender sender)
    {
        var orderId = Guid.NewGuid();
        app.Probe.FailCommitFor[orderId] = true;

        try
        {
            await CreateOrder(sender, orderId, expectFailure: true);
        }
        finally
        {
            app.Probe.FailCommitFor.TryRemove(orderId, out _);
        }

        // Just before the commit, the order and the outbox message were both in the one EF transaction...
        Assert.Equal(new InTransactionSnapshot(Orders: 1, OutboxMessages: 1), app.Probe.Snapshots[orderId]);
        // ...and rolling that transaction back removed both
        await AssertNothingCommitted(orderId);
    }

    private async Task CreateOrder(Sender sender, Guid orderId, bool simulateFailure = false, bool expectFailure = false)
    {
        if (sender == Sender.Mediator)
        {
            var send = () => SendThroughMediator(new CreateOrderCommand(10) { Id = orderId, SimulateFailure = simulateFailure });
            if (expectFailure)
            {
                await Assert.ThrowsAnyAsync<Exception>(send);
            }
            else
            {
                await send();
            }
            return;
        }

        await app.SendToClient(new CreateOrderFromMessage(orderId, simulateFailure));
        if (expectFailure)
        {
            // Rebus retries the failing message and then moves it to the error queue
            await Wait.Until(async () => await OrdersDatabase.CountErrorQueueMessages(app.RebusConnectionString, orderId) == 1,
                "the message is in the error queue");
        }
    }

    private async Task SendThroughMediator(CreateOrderCommand command)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    private async Task AssertNothingCommitted(Guid orderId)
    {
        Assert.Equal(0, await OrdersDatabase.CountOrders(app.OrdersConnectionString, orderId));
        Assert.Equal(0, await OrdersDatabase.CountOutboxMessages(app.OrdersConnectionString, orderId));
        await Task.Delay(Wait.QuietPeriod, TestContext.Current.CancellationToken);
        Assert.False(app.Probe.WasOrderCreatedHandled(orderId), "OrderCreated was handled although its command rolled back");
    }
}
