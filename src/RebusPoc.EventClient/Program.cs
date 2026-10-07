using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Rebus.Config;
using RebusPoc.Contracts;
using RebusPoc.EventClient;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Rebus")
    ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Rebus'.");

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

await builder.Build().RunAsync();
