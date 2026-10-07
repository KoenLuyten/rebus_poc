using MediatR;
using RebusPoc.EventClient.Messaging;

namespace RebusPoc.EventClient.Orders.Commands;

public record CreateOrderCommand(Guid Id, decimal Amount, DateTimeOffset PlacedAt) : ICommand<OrderDto>;

public class CreateOrderHandler(OrdersDbContext db) : IRequestHandler<CreateOrderCommand, OrderDto>
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

        var order = new Order { Id = command.Id, Amount = command.Amount, PlacedAt = command.PlacedAt };
        db.Orders.Add(order);
        return order.ToDto();
    }
}
