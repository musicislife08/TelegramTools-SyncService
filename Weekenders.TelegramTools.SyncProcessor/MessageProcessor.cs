using Weekenders.TelegramTools.Data;
using Weekenders.TelegramTools.Data.Models;
using Weekenders.TelegramTools.Telegram;

namespace Weekenders.TelegramTools.SyncProcessor;

public class MessageProcessor : BackgroundService
{
    private readonly ILogger<MessageProcessor> _logger;
    private readonly ITelegramService _telegramService;
    private readonly IDataService _dataService;

    public MessageProcessor(ILogger<MessageProcessor> logger, ITelegramService telegramService, IDataService dataService)
    {
        _logger = logger;
        _telegramService = telegramService;
        _dataService = dataService;
        _logger.LogInformation("{Name} Initialized", nameof(MessageProcessor));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Name} running at: {Time}", nameof(MessageProcessor), DateTimeOffset.UtcNow);
        await _telegramService.InitialSetup(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Getting message to process");
            var msg = await _dataService.GetFirstMessageAsync();
            try
            {
                if (msg is null)
                {
                    var wait = TimeSpan.FromSeconds(10);
                    _logger.LogDebug("Queue is empty.  Waiting {Seconds}s to grab the next message", wait.Seconds);
                    await Task.Delay(wait, stoppingToken);
                    continue;
                }

                _logger.LogInformation("Processing Message Id: {Id} Name: {Name}", msg.Id, msg.Name);
                await _telegramService.ProcessMessage(msg.TelegramId);
                _logger.LogInformation("Updating queue with result for {Id}", msg.TelegramId);
                await _dataService.UpdateMessageStatusAsync(msg, ProcessStatus.Processed);
            }
            catch (Exception e)
            {
                if (msg is null)
                    throw;
                msg.ExceptionMessage = e.Message;
                await _dataService.UpdateMessageStatusAsync(msg, ProcessStatus.Errored);
                _logger.LogError(e, "{Message}", e.Message);
            }
        }
    }
}