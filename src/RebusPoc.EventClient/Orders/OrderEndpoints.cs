using MediatR;
using RebusPoc.EventClient.Orders.Commands;
using RebusPoc.EventClient.Orders.Queries;

namespace RebusPoc.EventClient.Orders;

public record OrderRequest(decimal Amount);

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders");

        orders.MapGet("/", (IMediator mediator, CancellationToken ct) =>
            mediator.Send(new ListOrdersQuery(), ct));

        orders.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
            await mediator.Send(new GetOrderQuery(id), ct) is { } order ? Results.Ok(order) : Results.NotFound())
            .WithName("GetOrder");

        orders.MapPost("/", async (OrderRequest request, IMediator mediator, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var order = await mediator.Send(new CreateOrderCommand(Guid.NewGuid(), request.Amount, timeProvider.GetUtcNow()), ct);
            return Results.CreatedAtRoute("GetOrder", new { id = order.Id }, order);
        });

        orders.MapPut("/{id:guid}", async (Guid id, OrderRequest request, IMediator mediator, CancellationToken ct) =>
            await mediator.Send(new UpdateOrderCommand(id, request.Amount), ct) is { } order ? Results.Ok(order) : Results.NotFound());

        orders.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
            await mediator.Send(new DeleteOrderCommand(id), ct) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
