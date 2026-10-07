using System.Transactions;
using MediatR;
using Microsoft.Extensions.Logging;
using RebusPoc.EventClient.Orders;

namespace RebusPoc.EventClient.Messaging;

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
        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            var response = await next();
            await db.SaveChangesAsync(cancellationToken);
            scope.Complete();
            logger.LogInformation("Committed transaction for {Request}", requestName);
            return response;
        }
        catch (Exception ex)
        {
            // Disposing the scope without Complete() rolls the transaction back
            logger.LogWarning(ex, "Rolled back transaction for {Request}", requestName);
            throw;
        }
    }
}
