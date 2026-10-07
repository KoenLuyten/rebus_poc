using System.Text.Json;
using MediatorEndpoint.JsonRpc;
using MediatR;

namespace RebusPoc.EventClient.Endpoints;

public static class JsonRpcEndpoint
{
    public static IEndpointRouteBuilder MapJsonRpc(this IEndpointRouteBuilder app)
    {
        app.MapPost("/jsonrpc", async (HttpContext context, ISender sender, JsonRpc? jsonRpc, ILogger<JsonRpc> logger, CancellationToken cancellationToken) =>
        {
            // JsonRpcValidationFilter has already answered when the envelope is invalid, so jsonRpc is set here
            var id = jsonRpc!.Request.Id;

            try
            {
                var message = await jsonRpc.CreateMessageAsync(context);
                var response = await sender.Send(message!, cancellationToken);
                return JsonRpcResults.Response(id, response);
            }
            catch (Exception exc) when (exc is JsonException or ArgumentException)
            {
                return JsonRpcResults.Response(JsonRpcErrorResponse.Create(id, JsonRpcErrorCode.InvalidParams, exc.Message));
            }
            catch (Exception exc)
            {
                logger.LogError(exc, "JSON-RPC method {Method} failed", jsonRpc.Request.Method);
                return JsonRpcResults.Response(JsonRpcErrorResponse.InternalError(id, exc));
            }
        })
        .AddEndpointFilter<JsonRpcValidationFilter>();

        return app;
    }
}
