using Microsoft.EntityFrameworkCore.Migrations;

namespace Dwh.IS4Host.Data.Migrations.AspNetIdentity.ApplicationDb
{
    public partial class add_organization_contact_pointperson : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PointPerson",
                table: "OrganizationContactses",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointPerson",
                table: "OrganizationContactses");
        }
    }
}
