using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weekenders.TelegramTools.Data.Migrations
{
    public partial class AddName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Messages");
        }
    }
}
