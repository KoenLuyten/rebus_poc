using MediatR;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using RebusPoc.Contracts;
using RebusPoc.EventClient.Orders.Commands;

namespace RebusPoc.EventClient;

public class OrderPlacedHandler(IMediator mediator, ILogger<OrderPlacedHandler> logger) : IHandleMessages<OrderPlaced>
{
    public async Task Handle(OrderPlaced message)
    {
        logger.LogInformation("Received OrderPlaced {OrderId} ({Amount}) at {OccurredAt}",
            message.OrderId, message.Amount, message.OccurredAt);

        // Same command (and transaction behavior) as POST /orders
        await mediator.Send(new CreateOrderCommand(message.OrderId, message.Amount, message.OccurredAt));
    }
}
