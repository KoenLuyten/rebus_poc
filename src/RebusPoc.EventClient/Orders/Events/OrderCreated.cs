namespace RebusPoc.EventClient.Orders.Events;

/// <summary>Domain event that <c>CreateOrderHandler</c> sends to its own queue through the Rebus outbox.</summary>
public record OrderCreated(Guid OrderId, decimal Amount, DateTimeOffset PlacedAt);
