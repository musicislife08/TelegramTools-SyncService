using Weekenders.TelegramTools.Data;
using Weekenders.TelegramTools.Data.Models;

namespace Weekenders.TelegramTools.SyncService;

public interface IMessageQueueService
{
    Task PutAsync(Message message);
    Task<Message?> PullAsync();
    Task PutErrorAsync(ErroredMessage message);
}

public class MessageQueueService: IMessageQueueService
{
    private readonly ILogger<MessageQueueService> _logger;
    private readonly IDbService _dbService;

    public MessageQueueService(ILogger<MessageQueueService> logger, IDbService dbService)
    {
        _logger = logger;
        _dbService = dbService;
        _logger.LogDebug("{Name} Initialized", nameof(MessageQueueService));
    }

    public async Task PutAsync(Message message)
    {
        _logger.LogInformation("Adding Message {Id} to Queue", message.TelegramId);
        await _dbService.AddMessageAsync(message);
    }

    public async Task PutErrorAsync(ErroredMessage message)
    {
        _logger.LogInformation("Adding message {Id} to error queue", message.TelegramId);
        await _dbService.AddErrorAsync(message);
    }

    public async Task<Message?> PullAsync()
    {
        _logger.LogDebug("Pulling message from queue");
        var msg = await _dbService.GetLatestMessageAsync();
        if (msg is not null)
            _logger.LogInformation("Pulled {Id} from queue", msg.TelegramId);
        else
          _logger.LogDebug("Queue Empty");
        return msg;
    }
}