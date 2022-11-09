using FluentMigrator;

namespace Weekenders.TelegramTools.Data.Migrations;

[Migration(001)]
public class Initial: Migration
{
    public override void Up()
    {
        Create.Table("messages")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("telegram_id").AsInt64().NotNullable().Unique()
            .WithColumn("created_datetime_offset").AsDateTimeOffset().NotNullable()
            .WithColumn("processed_datetime_offset").AsDateTimeOffset().Nullable()
            .WithColumn("name").AsString().Nullable()
            .WithColumn("status").AsInt32().NotNullable()
            .WithColumn("exception_message").AsString().Nullable();
        Create.UniqueConstraint("telegram_id_unique").OnTable("messages").Column("telegram_id");
    }

    public override void Down()
    {
        Delete.Table("messages");
    }
}