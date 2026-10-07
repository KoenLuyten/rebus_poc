namespace RebusPoc.EventClient.Orders;

public class Order
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public OrderDto ToDto() => new(Id, Amount, PlacedAt, UpdatedAt);
}
