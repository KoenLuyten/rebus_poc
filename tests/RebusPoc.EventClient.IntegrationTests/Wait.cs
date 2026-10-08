namespace RebusPoc.EventClient.IntegrationTests;

public static class Wait
{
    /// <summary>How long to wait for a message to get through the outbox and the queue.</summary>
    public static readonly TimeSpan Delivery = TimeSpan.FromSeconds(30);

    /// <summary>How long to wait before concluding that a domain event was never sent; the outbox forwarder runs every second.</summary>
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(3);

    public static async Task Until(Func<Task<bool>> condition, string description)
    {
        var deadline = DateTime.UtcNow + Delivery;
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting until {description}");
            }
            await Task.Delay(200);
        }
    }
}
