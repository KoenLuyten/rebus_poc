using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace RebusPoc.EventClient.Orders.Events;

// Serilog writes this handler's logs to the Orders.dbo.Logs table (see Program.cs), so a row there proves the event left the outbox
public class OrderCreatedHandler(ILogger<OrderCreatedHandler> logger) : IHandleMessages<OrderCreated>
{
    public Task Handle(OrderCreated message)
    {
        logger.LogInformation("Domain event OrderCreated handled for order {OrderId} ({Amount})", message.OrderId, message.Amount);
        return Task.CompletedTask;
    }
}
