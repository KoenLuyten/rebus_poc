using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Handlers;
using RebusPoc.Contracts;
using RebusPoc.EventClient.IntegrationTests.Hooks;
using RebusPoc.EventClient.Orders;
using RebusPoc.EventClient.Orders.Events;
using Testcontainers.MsSql;

namespace RebusPoc.EventClient.IntegrationTests;

[CollectionDefinition(Name)]
public class AppCollection : ICollectionFixture<AppFixture>
{
    public const string Name = "EventClient";
}

/// <summary>
/// Starts SQL Server in a container and the real EventClient on top of it (EF, MediatR, Rebus with the outbox), plus
/// the test hooks. A one-way Rebus client plays the EventProvider and sends messages to the client's queue.
/// </summary>
public class AppFixture : IAsyncLifetime
{
    public const string ClientQueue = "event-client";

    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private WebApplicationFactory<Program>? _factory;
    private IBus? _provider;

    public TestProbe Probe { get; } = new();
    public string RebusConnectionString { get; private set; } = "";
    public string OrdersConnectionString { get; private set; } = "";
    public IServiceProvider Services => _factory!.Services;

    public async ValueTask InitializeAsync()
    {
        await _sqlServer.StartAsync();
        RebusConnectionString = _sqlServer.GetConnectionString();
        OrdersConnectionString = new SqlConnectionStringBuilder(RebusConnectionString) { InitialCatalog = "Orders" }.ConnectionString;

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Rebus", RebusConnectionString);
            builder.UseSetting("ConnectionStrings:Orders", OrdersConnectionString);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(Probe);
                services.AddSingleton<FailingCommitInterceptor>();
                services.ConfigureDbContext<OrdersDbContext>((provider, options) =>
                    options.AddInterceptors(provider.GetRequiredService<FailingCommitInterceptor>()));
                // Registered after the app's handlers, so Rebus runs it after OrderPlacedHandler
                services.AddTransient<IHandleMessages<OrderPlaced>, FailingOrderPlacedHandler>();
                services.AddTransient<IHandleMessages<OrderCreated>, OrderCreatedProbeHandler>();
                services.AddTransient<IHandleMessages<CreateOrderFromMessage>, CreateOrderFromMessageHandler>();
            });
        });

        // Starts the host, which starts the Rebus bus and creates its tables
        _ = _factory.Services;
        await WaitForClientQueue();

        _provider = Configure.OneWayClient()
            .Transport(t => t.UseSqlServerAsOneWayClient(new SqlServerTransportOptions(RebusConnectionString)))
            .Start();
    }

    public Task SendToClient(object message) => _provider!.Advanced.Routing.Send(ClientQueue, message);

    public async ValueTask DisposeAsync()
    {
        _provider?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        await _sqlServer.DisposeAsync();
    }

    private async Task WaitForClientQueue()
    {
        await using var connection = new SqlConnection(RebusConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand($"SELECT OBJECT_ID('dbo.[{ClientQueue}]')", connection);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (await command.ExecuteScalarAsync() is DBNull)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Rebus didn't create the '{ClientQueue}' queue table");
            }
            await Task.Delay(200);
        }
    }
}
