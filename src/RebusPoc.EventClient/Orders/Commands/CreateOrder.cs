using System.Text.Json.Serialization;
using MediatR;
using Rebus.Bus;
using RebusPoc.EventClient.Messaging;
using RebusPoc.EventClient.Orders.Events;

namespace RebusPoc.EventClient.Orders.Commands;

public record CreateOrderCommand(decimal Amount) : ICommand<OrderDto>
{
    // Generated on creation; JSON-RPC callers can't set it, the OrderPlaced handler overrides it with the event's id
    [JsonIgnore]
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonIgnore]
    public DateTimeOffset? PlacedAt { get; init; }

    // For testing the outbox: throws after OrderCreated was sent, so the order and the event must both roll back
    public bool SimulateFailure { get; init; }
}

public class CreateOrderHandler(OrdersDbContext db, IBus bus, TimeProvider timeProvider) : IRequestHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        if (command.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command), command.Amount, "Amount must not be negative.");
        }

        // Idempotent: Rebus delivers at least once, so the same OrderPlaced can arrive twice
        var existing = await db.Orders.FindAsync([command.Id], cancellationToken);
        if (existing is not null)
        {
            return existing.ToDto();
        }

        var order = new Order { Id = command.Id, Amount = command.Amount, PlacedAt = command.PlacedAt ?? timeProvider.GetUtcNow() };
        db.Orders.Add(order);

        // Goes to the Rebus outbox in the command's transaction, so it's only forwarded once the order is committed
        await bus.SendLocal(new OrderCreated(order.Id, order.Amount, order.PlacedAt));

        if (command.SimulateFailure)
        {
            throw new InvalidOperationException($"Simulated failure after dispatching OrderCreated for order {order.Id}");
        }

        return order.ToDto();
    }
}
