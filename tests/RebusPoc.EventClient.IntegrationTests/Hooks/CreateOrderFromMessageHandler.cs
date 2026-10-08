using MediatR;
using Rebus.Handlers;
using RebusPoc.EventClient.Orders.Commands;

namespace RebusPoc.EventClient.IntegrationTests.Hooks;

/// <summary>Test message that makes the app send a <see cref="CreateOrderCommand"/> from inside a Rebus handler.</summary>
public record CreateOrderFromMessage(Guid OrderId, bool SimulateFailure);

/// <summary>
/// Does what <c>OrderPlacedHandler</c> does (send the command through MediatR from a Rebus handler), but can also set
/// <see cref="CreateOrderCommand.SimulateFailure"/>.
/// </summary>
public class CreateOrderFromMessageHandler(IMediator mediator) : IHandleMessages<CreateOrderFromMessage>
{
    public Task Handle(CreateOrderFromMessage message) =>
        mediator.Send(new CreateOrderCommand(10) { Id = message.OrderId, SimulateFailure = message.SimulateFailure });
}
