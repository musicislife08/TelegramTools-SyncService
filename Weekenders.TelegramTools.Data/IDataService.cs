using Weekenders.TelegramTools.Data.Models;

namespace Weekenders.TelegramTools.Data;

public interface IDataService
{
    Task AddMessageAsync(Message message);
    Task UpdateMessageStatusAsync(Message message, ProcessStatus status);
    Task<Message?> GetFirstMessageAsync();
    Task DeleteOldProcessedMessages(int daysToKeep);
}