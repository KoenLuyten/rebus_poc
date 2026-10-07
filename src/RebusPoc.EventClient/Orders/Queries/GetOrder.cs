using MediatR;
using Microsoft.EntityFrameworkCore;
using RebusPoc.EventClient.Messaging;

namespace RebusPoc.EventClient.Orders.Queries;

public record GetOrderQuery(Guid Id) : IQuery<OrderDto?>;

public class GetOrderHandler(OrdersDbContext db) : IRequestHandler<GetOrderQuery, OrderDto?>
{
    public Task<OrderDto?> Handle(GetOrderQuery query, CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking()
            .Where(o => o.Id == query.Id)
            .Select(o => new OrderDto(o.Id, o.Amount, o.PlacedAt, o.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
}
