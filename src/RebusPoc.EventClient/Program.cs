using Microsoft.EntityFrameworkCore;
using Rebus.Config;
using RebusPoc.Contracts;
using RebusPoc.EventClient;
using RebusPoc.EventClient.Messaging;
using RebusPoc.EventClient.Orders;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Rebus")
    ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Rebus'.");
var ordersConnectionString = builder.Configuration.GetConnectionString("Orders")
    ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Orders'.");

// No EnableRetryOnFailure: EF's retrying execution strategy doesn't support the ambient TransactionScope
builder.Services.AddDbContext<OrdersDbContext>(o => o.UseSqlServer(ordersConnectionString));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<OrdersDbContext>();
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
});

builder.Services.AddRebus(
    configure => configure
        .Transport(t => t.UseSqlServer(new SqlServerTransportOptions(connectionString), "event-client"))
        .Subscriptions(s => s.StoreInSqlServer(connectionString, "RebusSubscriptions", isCentralized: true)),
    onCreated: async bus =>
    {
        await bus.Subscribe<OrderPlaced>();
        Console.WriteLine("Subscribed to OrderPlaced");
    });

builder.Services.AutoRegisterHandlersFromAssemblyOf<OrderPlacedHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.EnsureCreatedAsync();
}

app.MapOrderEndpoints();

await app.RunAsync();
