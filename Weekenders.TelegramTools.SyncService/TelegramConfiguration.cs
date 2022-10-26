namespace Weekenders.TelegramTools.SyncService;

public class TelegramConfiguration
{
    public string? PhoneNumber { get; set; } = string.Empty;
    public string SessionPath { get; set; } = "/session";
    public string? SourceGroupName { get; set; } = string.Empty;
    public string? DestinationGroupName { get; set; } = string.Empty;
}