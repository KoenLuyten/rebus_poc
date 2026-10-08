using MediatorEndpoint;
using Microsoft.EntityFrameworkCore;
using Rebus.Config;
using Rebus.Config.Outbox;
using Serilog;
using Serilog.Filters;
using Serilog.Sinks.MSSqlServer;
using RebusPoc.Contracts;
using RebusPoc.EventClient;
using RebusPoc.EventClient.Endpoints;
using RebusPoc.EventClient.Messaging;
using RebusPoc.EventClient.Orders;
using RebusPoc.EventClient.Orders.Events;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Rebus")
    ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Rebus'.");
var ordersConnectionString = builder.Configuration.GetConnectionString("Orders")
    ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Orders'.");

// Create the Orders database before Serilog and Rebus add their Logs and Outbox tables to it. EnsureCreated skips
// creating the schema when the database already has tables
await using (var setupDb = new OrdersDbContext(new DbContextOptionsBuilder<OrdersDbContext>().UseSqlServer(ordersConnectionString).Options))
{
    await setupDb.Database.EnsureCreatedAsync();
}

// Only the domain event handler's logs go to the database, so Orders.dbo.Logs shows which OrderCreated events were handled
builder.Logging.AddSerilog(new LoggerConfiguration()
    .Filter.ByIncludingOnly(Matching.FromSource<OrderCreatedHandler>())
    .WriteTo.MSSqlServer(ordersConnectionString, new MSSqlServerSinkOptions
    {
        TableName = "Logs",
        AutoCreateSqlTable = true,
        BatchPeriod = TimeSpan.FromSeconds(1),
    })
    .CreateLogger(), dispose: true);

// No EnableRetryOnFailure: EF's retrying execution strategy doesn't support user-initiated transactions
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

// OpenAPI document for the JSON-RPC methods, served at GET /openapi (?format=yaml for YAML)
builder.Services.AddJsonRpcOpenApi(cfg => cfg.PostProcess = document =>
{
    document.Info.Title = "Rebus POC Orders API";
    document.Info.Version = "1.0";
    document.Info.Description =
        "JSON-RPC 2.0 API. Each path below documents one method (path /Orders/CreateOrder is method \"Orders.CreateOrder\"), " +
        "but every call is sent as POST /jsonrpc with the method name in the envelope.";
});

builder.Services.AddRebus(
    configure => configure
        .Transport(t => t.UseSqlServer(new SqlServerTransportOptions(connectionString), "event-client"))
        .Subscriptions(s => s.StoreInSqlServer(connectionString, "RebusSubscriptions", isCentralized: true))
        // The outbox lives in the Orders database, so EF and Rebus can share one SqlConnection and transaction
        .Outbox(o => o.StoreInSqlServer(ordersConnectionString, "Outbox")),
    onCreated: async bus =>
    {
        await bus.Subscribe<OrderPlaced>();
        Console.WriteLine("Subscribed to OrderPlaced");
    });

builder.Services.AutoRegisterHandlersFromAssemblyOf<OrderPlacedHandler>();

var app = builder.Build();

app.MapJsonRpc();
app.UseJsonRpcOpenApi();

// Swagger UI for the generated document. The documented paths (/Orders/CreateOrder) are documentation only,
// so "Try it out" requests are rewritten to POST /jsonrpc; the body already contains the JSON-RPC envelope
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi", "Orders JSON-RPC");
    options.RoutePrefix = "swagger";
    options.UseRequestInterceptor(
        "(request) => { if (request.method === 'POST') { request.url = new URL('/jsonrpc', request.url).href; } return request; }");
});

await app.RunAsync();
