using Weekenders.TelegramTools.Data.Models;

namespace Weekenders.TelegramTools.Data;

public interface IDbService
{
    Task<Message?> GetLatestMessageAsync();
    Task AddMessageAsync(Message message);
}