namespace RebusPoc.Contracts;

public record OrderPlaced(Guid OrderId, decimal Amount, DateTimeOffset OccurredAt);
