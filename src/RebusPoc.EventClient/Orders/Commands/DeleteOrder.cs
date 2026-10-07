using MediatR;
using RebusPoc.EventClient.Messaging;

namespace RebusPoc.EventClient.Orders.Commands;

public record DeleteOrderCommand(Guid Id) : ICommand<bool>;

public class DeleteOrderHandler(OrdersDbContext db) : IRequestHandler<DeleteOrderCommand, bool>
{
    public async Task<bool> Handle(DeleteOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await db.Orders.FindAsync([command.Id], cancellationToken);
        if (order is null)
        {
            return false;
        }

        db.Orders.Remove(order);
        return true;
    }
}
