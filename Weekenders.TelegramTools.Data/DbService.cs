using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Weekenders.TelegramTools.Data.Models;

namespace Weekenders.TelegramTools.Data;

public class DbService: IDbService
{
    private readonly ILogger<DbService> _logger;
    private readonly IDbContextFactory<MessageDbContext> _contextFactory;

    public DbService(ILogger<DbService> logger, IDbContextFactory<MessageDbContext> factory)
    {
        _logger = logger;
        _contextFactory = factory;
        _logger.LogDebug("{Name} Initialized", nameof(DbService));
    }

    public async Task<Message?> GetLatestMessageAsync()
    {
        _logger.LogTrace("{Name} Called", nameof(GetLatestMessageAsync));
        await using var context = await _contextFactory.CreateDbContextAsync();
        var latest = await context.Messages.OrderBy(x => x.CreatedDateTimeOffset).FirstOrDefaultAsync();
        if (latest is null)
        {
            _logger.LogDebug("No Messages Found.  Queue Empty");
            return latest;
        }

        _logger.LogInformation("Pulled Message: {Id}", latest.TelegramId);
        context.Messages.Remove(latest);
        await context.SaveChangesAsync();
        _logger.LogDebug("Removed {Id} from database", latest.TelegramId);
        return latest;
    }

    public async Task AddMessageAsync(Message? message)
    {
        _logger.LogTrace("{Name} Called", nameof(AddMessageAsync));
        ArgumentNullException.ThrowIfNull(message);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var msg = await CheckMessage(message, context);
        if (msg is null)
        {
            _logger.LogInformation("Message {Id} Already Exists", message.TelegramId);
            return;
        }

        await context.Messages.AddAsync(msg);
        await context.SaveChangesAsync();
        _logger.LogDebug("Added {Id} to database", msg.TelegramId);
    }

    private static async Task<Message?> CheckMessage(Message? message, MessageDbContext context)
    {
        ArgumentNullException.ThrowIfNull(message);
        var exists = await context.Messages.AnyAsync(x => x.TelegramId == message.TelegramId);
        if (exists)
            return null;
        message.CreatedDateTimeOffset ??= DateTimeOffset.UtcNow;
        return message;
    }
}