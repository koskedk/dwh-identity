using Microsoft.EntityFrameworkCore.Migrations;

namespace Dwh.IS4Host.Data.Migrations.AspNetIdentity.ApplicationDb
{
    public partial class add_subscribe_to_newsletter : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SubscribeToNewsletter",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscribeToNewsletter",
                table: "AspNetUsers");
        }
    }
}
