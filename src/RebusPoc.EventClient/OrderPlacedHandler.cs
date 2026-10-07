using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using RebusPoc.Contracts;

namespace RebusPoc.EventClient;

public class OrderPlacedHandler(ILogger<OrderPlacedHandler> logger) : IHandleMessages<OrderPlaced>
{
    public Task Handle(OrderPlaced message)
    {
        logger.LogInformation("Received OrderPlaced {OrderId} ({Amount}) at {OccurredAt}",
            message.OrderId, message.Amount, message.OccurredAt);
        return Task.CompletedTask;
    }
}
