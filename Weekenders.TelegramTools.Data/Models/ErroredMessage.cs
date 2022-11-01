namespace Weekenders.TelegramTools.Data.Models;

public class ErroredMessage : Message
{
    public string? ExceptionMessage { get; set; }
}