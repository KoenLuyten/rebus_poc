using System.Data;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Rebus.Config.Outbox;
using Rebus.Pipeline;
using Rebus.SqlServer.Outbox;
using Rebus.Transport;
using RebusPoc.EventClient.Orders;

namespace RebusPoc.EventClient.Messaging;

/// <summary>
/// Runs every command in one SQL transaction that EF and the Rebus outbox share, so the order and the messages the
/// handler sends are committed or rolled back together.
/// </summary>
public class TransactionBehavior<TRequest, TResponse>(OrdersDbContext db, ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Key under which Rebus keeps the outbox connection of the message being handled
    private const string OutboxConnectionKey = "current-outbox-connection";

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICommand)
        {
            return await next();
        }

        return MessageContext.Current?.TransactionContext.Items.TryGetValue(OutboxConnectionKey, out var item) == true
            && item is OutboxConnection outboxConnection
                ? await HandleInRebusHandler(outboxConnection, next, cancellationToken)
                : await HandleWithOwnTransaction(next, cancellationToken);
    }

    // Inside a Rebus handler, Rebus owns the outbox connection and transaction. It commits them once the message is
    // handled, and rolls them back (and retries the message) when the handler throws.
    private async Task<TResponse> HandleInRebusHandler(
        OutboxConnection outboxConnection, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        db.Database.SetDbConnection(outboxConnection.Connection);
        await db.Database.UseTransactionAsync(outboxConnection.Transaction, cancellationToken);

        try
        {
            var response = await next();
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Saved {Request} in the Rebus message transaction; Rebus commits it", requestName);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rolled back transaction for {Request}", requestName);
            throw;
        }
    }

    // Outside Rebus (JSON-RPC), the behavior owns the transaction and hands it to Rebus with UseOutbox
    private async Task<TResponse> HandleWithOwnTransaction(RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        using var rebusScope = new RebusTransactionScope();
        rebusScope.UseOutbox((SqlConnection)db.Database.GetDbConnection(), (SqlTransaction)transaction.GetDbTransaction());

        try
        {
            var response = await next();
            await db.SaveChangesAsync(cancellationToken);
            // Writes the messages the handler sent to the outbox table, inside the same transaction
            await rebusScope.CompleteAsync();
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Committed transaction for {Request}", requestName);
            return response;
        }
        catch (Exception ex)
        {
            // Disposing the transaction without committing rolls back the order and the outbox messages
            logger.LogWarning(ex, "Rolled back transaction for {Request}", requestName);
            throw;
        }
    }
}
