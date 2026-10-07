using MediatR;
using RebusPoc.EventClient.Messaging;

namespace RebusPoc.EventClient.Orders.Commands;

public record UpdateOrderCommand(Guid Id, decimal Amount) : ICommand<OrderDto?>;

public class UpdateOrderHandler(OrdersDbContext db, TimeProvider timeProvider) : IRequestHandler<UpdateOrderCommand, OrderDto?>
{
    public async Task<OrderDto?> Handle(UpdateOrderCommand command, CancellationToken cancellationToken)
    {
        if (command.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command), command.Amount, "Amount must not be negative.");
        }

        var order = await db.Orders.FindAsync([command.Id], cancellationToken);
        if (order is null)
        {
            return null;
        }

        order.Amount = command.Amount;
        order.UpdatedAt = timeProvider.GetUtcNow();
        return order.ToDto();
    }
}
