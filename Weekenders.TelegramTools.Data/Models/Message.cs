// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Weekenders.TelegramTools.Data.Models;

// ReSharper disable once ClassNeverInstantiated.Global
public class Message
{
    public long Id { get; set; }
    public long SourceId { get; set; }
    public long DestinationId { get; set; }
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
    public string? Name { get; set; }
    public ProcessStatus Status { get; set; } = ProcessStatus.Queued;
    public string? ExceptionMessage { get; set; }
}