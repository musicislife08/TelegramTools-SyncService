namespace Weekenders.TelegramTools.Telegram;

public interface ITelegramService
{
    Task<TelegramProcessResult> ProcessMessage(long id);
    Task InitialSetup(bool enableUpdate);
}