using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rebus.Config;
using RebusPoc.EventProvider;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Rebus")
    ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Rebus'.");

builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseSqlServerAsOneWayClient(new SqlServerTransportOptions(connectionString)))
    .Subscriptions(s => s.StoreInSqlServer(connectionString, "RebusSubscriptions", isCentralized: true)));

builder.Services.AddHostedService<PublisherWorker>();

await builder.Build().RunAsync();
