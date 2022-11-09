namespace Weekenders.TelegramTools.Telegram;

public interface ITelegramService
{
    Task ProcessMessages();
    Task ProcessMessage(long id);
    Task InitialSetup(bool enableUpdate);
}