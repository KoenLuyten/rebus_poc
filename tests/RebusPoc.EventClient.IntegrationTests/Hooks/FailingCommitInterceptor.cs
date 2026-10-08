using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RebusPoc.EventClient.IntegrationTests.Hooks;

/// <summary>
/// Fails EF's commit for armed orders. By then <c>TransactionBehavior</c> has saved the order and completed the Rebus
/// scope, so the outbox message is written too. If Rebus used its own transaction, that message would survive the rollback.
/// </summary>
public class FailingCommitInterceptor(TestProbe probe) : DbTransactionInterceptor
{
    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        var sqlTransaction = (SqlTransaction)transaction;
        foreach (var orderId in probe.FailCommitFor.Keys)
        {
            var orders = await OrdersDatabase.CountOrders(sqlTransaction.Connection!, sqlTransaction, orderId);
            if (orders == 0)
            {
                continue;
            }

            var outboxMessages = await OrdersDatabase.CountOutboxMessages(sqlTransaction.Connection!, sqlTransaction, orderId);
            probe.Snapshots[orderId] = new InTransactionSnapshot(orders, outboxMessages);
            throw new InvalidOperationException($"Simulated commit failure for order {orderId}");
        }

        return result;
    }
}
