using MediatorEndpoint;
using Microsoft.EntityFrameworkCore;
using Rebus.Config;
using RebusPoc.Contracts;
using RebusPoc.EventClient;
using RebusPoc.EventClient.Endpoints;
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

// Exposes every Orders request as a JSON-RPC method, e.g. CreateOrderCommand -> "Orders.CreateOrder"
builder.Services.AddMediatorEndpoint(cfg =>
{
    cfg.RegisterServicesFromAssemblies(typeof(OrdersDbContext).Assembly);
    cfg.RequestEvaluator = type => type.Namespace?.StartsWith("RebusPoc.EventClient.Orders") == true;
    cfg.RequestName = type => new RequestName(null, "Orders", type.Name.Replace("Command", "").Replace("Query", ""));
    cfg.RequestKind = type => typeof(ICommand).IsAssignableFrom(type) ? RequestKind.Command : RequestKind.Query;
    cfg.VerifyRequestKind = true;
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

app.MapJsonRpc();

await app.RunAsync();
