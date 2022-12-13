using FluentMigrator;

namespace Weekenders.TelegramTools.Data.Migrations;

[Migration(003)]
public class ModifyMessageLayout: Migration
{
    public override void Up()
    {
        Rename
            .Column("last_modified")
            .OnTable("messages")
            .To("modified");
        Rename.Column("telegram_id")
            .OnTable("messages")
            .To("source_id");
        Alter.Table("messages").AddColumn("destination_id")
            .AsInt64()
            .Nullable()
            .Unique()
            .Indexed();
        Alter.Column("source_id")
            .OnTable("messages")
            .AsInt64()
            .Unique()
            .Indexed()
            .NotNullable();
        Delete.UniqueConstraint("telegram_id_unique").FromTable("messages");
        Create.UniqueConstraint("source_id_unique").OnTable("messages").Column("source_id");
        Create.UniqueConstraint("destination_id_unique").OnTable("messages").Column("destination_id");
    }

    public override void Down()
    {
        Rename.Column("modified")
            .OnTable("messages")
            .To("last_modified");
        Rename.Column("source_id")
            .OnTable("messages")
            .To("telegram_id");
        Delete.Column("destination_id")
            .FromTable("messages");
        Delete.UniqueConstraint("source_id_unique").FromTable("messages");
        Delete.UniqueConstraint("destination_id_unique").FromTable("messages");
        Create.UniqueConstraint("telegram_id_unique").OnTable("messages").Column("telegram_id");
    }
}