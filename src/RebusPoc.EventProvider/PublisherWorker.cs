using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using RebusPoc.Contracts;

namespace RebusPoc.EventProvider;

public class PublisherWorker(IBus bus, ILogger<PublisherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var evt = new OrderPlaced(Guid.NewGuid(), Math.Round((decimal)Random.Shared.NextDouble() * 100, 2), DateTimeOffset.UtcNow);
            await bus.Publish(evt);
            logger.LogInformation("Published OrderPlaced {OrderId} ({Amount})", evt.OrderId, evt.Amount);
        }
    }
}
