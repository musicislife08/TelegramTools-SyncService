namespace Weekenders.TelegramTools.Telegram.Exceptions;

public class ClientInitializationException: Exception
{
    public ClientInitializationException(): base("Cannot processes messages without initialization"){}
}