using Dapper.FluentMap.Mapping;
using Weekenders.TelegramTools.Data.Models;

namespace Weekenders.TelegramTools.Data;

public class MessageMapper: EntityMap<Message>
{
    internal MessageMapper()
    {
        Map(m => m.Id).ToColumn("id");
        Map(m => m.TelegramId).ToColumn("telegram_id");
        Map(m => m.CreatedDateTimeOffset).ToColumn("created_datetime_offset");
        Map(m => m.ProcessedDateTimeOffset).ToColumn("processed_datetime_offset");
        Map(m => m.Name).ToColumn("name");
        Map(m => m.Status).ToColumn("status");
        Map(m => m.ExceptionMessage).ToColumn("exception_message");
    }
}