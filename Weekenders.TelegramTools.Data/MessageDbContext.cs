using Microsoft.EntityFrameworkCore;
using Weekenders.TelegramTools.Data.Models;

namespace Weekenders.TelegramTools.Data;

public class MessageDbContext: DbContext
{
    public MessageDbContext(DbContextOptions<MessageDbContext> options) : base(options){}
    public DbSet<Message> Messages { get; set; }
    public DbSet<ErroredMessage> ErroredMessages { get; set; }
}