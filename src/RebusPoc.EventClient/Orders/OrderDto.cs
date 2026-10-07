namespace RebusPoc.EventClient.Orders;

public record OrderDto(Guid Id, decimal Amount, DateTimeOffset PlacedAt, DateTimeOffset? UpdatedAt);
