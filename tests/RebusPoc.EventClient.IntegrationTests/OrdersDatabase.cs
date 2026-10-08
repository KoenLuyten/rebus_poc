using Microsoft.Data.SqlClient;

namespace RebusPoc.EventClient.IntegrationTests;

/// <summary>Raw SQL checks against the Orders database, the Rebus outbox in it and the Rebus error queue.</summary>
public static class OrdersDatabase
{
    public static Task<int> CountOrders(SqlConnection connection, SqlTransaction? transaction, Guid orderId) =>
        Count(connection, transaction, "SELECT COUNT(*) FROM dbo.Orders WHERE Id = @orderId", orderId);

    // The message body is the JSON-serialized OrderCreated, so it contains the order Id
    public static Task<int> CountOutboxMessages(SqlConnection connection, SqlTransaction? transaction, Guid orderId) =>
        Count(connection, transaction,
            "SELECT COUNT(*) FROM dbo.Outbox WHERE Headers LIKE '%OrderCreated%' AND CAST(Body AS varchar(max)) LIKE '%' + @orderId + '%'",
            orderId);

    public static async Task<int> CountOrders(string connectionString, Guid orderId)
    {
        await using var connection = await Open(connectionString);
        return await CountOrders(connection, null, orderId);
    }

    public static async Task<int> CountOutboxMessages(string connectionString, Guid orderId)
    {
        await using var connection = await Open(connectionString);
        return await CountOutboxMessages(connection, null, orderId);
    }

    public static async Task<int> CountErrorQueueMessages(string rebusConnectionString, Guid orderId)
    {
        await using var connection = await Open(rebusConnectionString);
        return await Count(connection, null,
            "SELECT COUNT(*) FROM dbo.error WHERE CAST(body AS varchar(max)) LIKE '%' + @orderId + '%'", orderId);
    }

    private static async Task<SqlConnection> Open(string connectionString)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<int> Count(SqlConnection connection, SqlTransaction? transaction, string sql, Guid orderId)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId.ToString());
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
