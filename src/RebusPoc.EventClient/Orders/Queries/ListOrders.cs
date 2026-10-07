using MediatR;
using Microsoft.EntityFrameworkCore;
using RebusPoc.EventClient.Messaging;

namespace RebusPoc.EventClient.Orders.Queries;

public record ListOrdersQuery : IQuery<IReadOnlyList<OrderDto>>;

public class ListOrdersHandler(OrdersDbContext db) : IRequestHandler<ListOrdersQuery, IReadOnlyList<OrderDto>>
{
    public async Task<IReadOnlyList<OrderDto>> Handle(ListOrdersQuery query, CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking()
            .OrderByDescending(o => o.PlacedAt)
            .Select(o => new OrderDto(o.Id, o.Amount, o.PlacedAt, o.UpdatedAt))
            .ToListAsync(cancellationToken);
}
