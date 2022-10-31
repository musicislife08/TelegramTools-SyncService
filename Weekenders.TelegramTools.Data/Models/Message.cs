using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Weekenders.TelegramTools.Data.Models;

// ReSharper disable once ClassNeverInstantiated.Global
public class Message
{
    [Key]
    public long Id { get; set; }

    [Required]
    public long TelegramId { get; set; }

    [Required]
    public DateTimeOffset? CreatedDateTimeOffset { get; set; }

    [Unicode]
    public string? Name { get; set; }
}