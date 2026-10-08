using System.Data;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Rebus.Config.Outbox;
using Rebus.Transport;
using RebusPoc.EventClient.Orders;

namespace RebusPoc.EventClient.Messaging;

/// <summary>
/// Runs every command in one SQL transaction that EF and the Rebus outbox share, so the order and the messages the
/// handler sends are committed or rolled back together.
/// </summary>
/// <remarks>
/// The behavior owns the transaction even inside a Rebus handler. It deliberately doesn't join the outbox transaction
/// Rebus opens for an incoming message: Rebus.SqlServer commits that one before it writes the outgoing messages to the
/// outbox, so the order and its events would be committed separately (see the integration tests).
/// </remarks>
public class TransactionBehavior<TRequest, TResponse>(OrdersDbContext db, ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICommand)
        {
            return await next();
        }

        var requestName = typeof(TRequest).Name;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        // Becomes the ambient Rebus transaction for the handler (also inside a Rebus handler), so its sends go to the outbox
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
