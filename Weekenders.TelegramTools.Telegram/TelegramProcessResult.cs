using Weekenders.TelegramTools.Data.Models;

namespace Weekenders.TelegramTools.Telegram;

public class TelegramProcessResult
{
    public ProcessStatus ProcessStatus { get; set; }
    public long? TelegramId { get; set; }
}