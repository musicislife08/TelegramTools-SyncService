using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Weekenders.TelegramTools.Data.Models;

namespace Weekenders.TelegramTools.Data;

public class DataService : IDataService
{
    private readonly ILogger<DataService> _logger;
    private readonly string _connectionString;

    public DataService(ILogger<DataService> logger, IOptions<DataConfig> options)
    {
        _logger = logger;
        ArgumentNullException.ThrowIfNull(options.Value);
        _connectionString = Extensions.GetConnectionString(options.Value);
        _logger.LogDebug("{Name} Initialized", nameof(DataService));
    }

    public async Task AddMessageAsync(Message message)
    {
        const string sql = "INSERT INTO public.messages (source_id, created, name, status) " +
                           "VALUES (@SourceId, @Created, @Name, 0) " +
                           "ON CONFLICT ON CONSTRAINT source_id_unique DO NOTHING";
        if (IsValidMessage(message))
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            var result = await connection.ExecuteAsync(sql, message, transaction);
            if (result is not 1)
            {
                _logger.LogError("Error saving ID:{Id} Name:{Name} to database", message.SourceId, message.Name);
                await transaction.DisposeAsync();
                return;
            }

            await transaction.CommitAsync();
        }
    }

    public async Task UpdateMessageStatusAsync(Message message, ProcessStatus status)
    {
        const string sql = "UPDATE public.messages SET status=@Status WHERE id=@Id";
        const string processedSql = "UPDATE public.messages SET destination_id=@DestinationId, modified=@Modified, status=@Status WHERE id=@Id";
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        if (!IsValidMessage(message))
        {
            _logger.LogError("Message {Id} is invalid.  Skipping", message.SourceId);
            await transaction.DisposeAsync();
            return;
        }

        int result;
        switch (status)
        {
            case ProcessStatus.Queued or ProcessStatus.Processing or ProcessStatus.Processed:
                break;
            case ProcessStatus.Errored or ProcessStatus.OtherError:
                break;
            case ProcessStatus.DeletedFromSource:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        if (status is ProcessStatus.Processed)
        {
            _logger.LogDebug("Setting Message {Id} status to Processed", message.SourceId);
            result = await connection.ExecuteAsync(processedSql, new {message.DestinationId, Modified=DateTimeOffset.UtcNow, Status = status, message.Id}, transaction);
        }
        else
        {
            _logger.LogDebug("Setting Message {Id} status to {Name}", message.SourceId, nameof(status));
            result = await connection.ExecuteAsync(sql, new {Modified=DateTimeOffset.UtcNow, Status = status, message.Id }, transaction);
        }

        if (result is not 1)
        {
            await transaction.RollbackAsync();
            _logger.LogError("Error updating {Id} status to {Name}", message.Id, nameof(status));
            return;
        }

        await transaction.CommitAsync();
    }

    public async Task DeleteOldProcessedMessages(int daysToKeep = -30)
    {
        var dt = DateTimeOffset.UtcNow.AddDays(daysToKeep);
        const string sql = "DELETE FROM messages WHERE status = 3 AND created > @Created";
        await using var connection = new NpgsqlConnection(_connectionString);
        var results = await connection.ExecuteAsync(sql, new { Created = dt });
        _logger.LogInformation("Removed {Count} Records older than {Date}", results, dt.ToLocalTime());
    }

    public async Task<Message?> GetFirstMessageAsync()
    {
        const string sql = "SELECT * FROM messages WHERE status <= 2 ORDER BY created LIMIT 1;";
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var result = await connection.QuerySingleOrDefaultAsync<Message?>(sql, null, transaction);
        if (result is not null && result.Id is not 0)
        {
            var updated = await connection.ExecuteAsync(
                $"UPDATE messages SET status=1, modified=@Modified WHERE id=@Id",
                new { result.Id, LastModified = DateTimeOffset.UtcNow},
                transaction);
            if (updated is not 1)
            {
                await transaction.RollbackAsync();
                return null;
            }
        }

        await transaction.CommitAsync();
        return result;
    }

    private static bool IsValidMessage(Message message)
    {
        return message.SourceId is not 0 && !string.IsNullOrWhiteSpace(message.Name);
    }
}