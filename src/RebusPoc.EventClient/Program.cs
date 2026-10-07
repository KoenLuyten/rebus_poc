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
