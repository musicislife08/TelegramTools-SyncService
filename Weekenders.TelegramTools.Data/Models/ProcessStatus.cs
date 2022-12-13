namespace Weekenders.TelegramTools.Data.Models;

public enum ProcessStatus
{
    Queued = 0,
    Processing = 1,
    Errored = 2,
    Processed = 3,
    DeletedFromSource = 4,
    OtherError = 5
}