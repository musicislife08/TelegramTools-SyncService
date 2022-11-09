using System.ComponentModel.DataAnnotations;

namespace Weekenders.TelegramTools.Data.Models;

// ReSharper disable once ClassNeverInstantiated.Global
public class Message
{
    [Key]
    public long Id { get; set; }

    [Required]
    public long TelegramId { get; set; }

    [Required]
    public DateTimeOffset CreatedDateTimeOffset { get; set; }

    public DateTimeOffset? ProcessedDateTimeOffset { get; set; }

    public string? Name { get; set; }

    [Required]
    public ProcessStatus Status { get; set; } = ProcessStatus.Queued;

    public string? ExceptionMessage { get; set; }
}