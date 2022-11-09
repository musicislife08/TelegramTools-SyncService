using Weekenders.TelegramTools.Telegram;

namespace Weekenders.TelegramTools.SyncListener;

public class TelegramWorker : BackgroundService
{
    private readonly ILogger<TelegramWorker> _logger;
    private readonly ITelegramService _telegramService;

    public TelegramWorker(ILogger<TelegramWorker> logger, ITelegramService telegramService)
    {
        _logger = logger;
        _telegramService = telegramService;
        _logger.LogInformation("{Name} Constructed", nameof(TelegramWorker));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Beginning Initial Setup");
        await _telegramService.InitialSetup(true);
        _logger.LogInformation("{Name} running at: {Time}", nameof(TelegramWorker), DateTimeOffset.Now);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(5000, stoppingToken);
        }
    }
}