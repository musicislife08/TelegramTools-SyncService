using Dapper.FluentMap.Mapping;
using Weekenders.TelegramTools.Data.Models;

namespace Weekenders.TelegramTools.Data;

public class MessageMapper: EntityMap<Message>
{
    internal MessageMapper()
    {
        Map(m => m.Id).ToColumn("id");
        Map(m => m.SourceId).ToColumn("source_id");
        Map(m => m.DestinationId).ToColumn("destination_id");
        Map(m => m.Created).ToColumn("created");
        Map(m => m.Modified).ToColumn("modified");
        Map(m => m.Name).ToColumn("name");
        Map(m => m.Status).ToColumn("status");
        Map(m => m.ExceptionMessage).ToColumn("exception_message");
    }
}