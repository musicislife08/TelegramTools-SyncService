using FluentMigrator;

namespace Weekenders.TelegramTools.Data.Migrations;

[Migration(002)]
public class MakeProcessedLastModified: Migration
{
    public override void Up()
    {
        Rename
            .Column("processed_datetime_offset")
            .OnTable("messages")
            .To("last_modified");
        Rename.Column("created_datetime_offset")
            .OnTable("messages")
            .To("created");
        Alter
            .Column("last_modified")
            .OnTable("messages")
            .AsDateTimeOffset()
            .NotNullable()
            .WithDefaultValue(RawSql.Insert("NOW()"));
    }

    public override void Down()
    {
        Alter.Column("last_modified")
            .OnTable("messages")
            .AsDateTimeOffset()
            .Nullable()
            .WithDefaultValue(null);
        Rename.Column("last_modified")
            .OnTable("messages")
            .To("processed_datetime_offset");
        Rename.Column("created")
            .OnTable("messages")
            .To("created_datetime_offset");
    }
}