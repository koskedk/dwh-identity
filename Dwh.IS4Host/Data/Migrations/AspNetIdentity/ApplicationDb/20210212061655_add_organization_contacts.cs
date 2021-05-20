using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dwh.IS4Host.Data.Migrations.AspNetIdentity.ApplicationDb
{
    public partial class add_organization_contacts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UsgMechanism",
                table: "Organizations",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizationContactses",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    OrganizationId = table.Column<Guid>(nullable: false),
                    Title = table.Column<string>(nullable: true),
                    Email = table.Column<string>(nullable: true),
                    Mobile = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationContactses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationContactses_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationContactses_OrganizationId",
                table: "OrganizationContactses",
                column: "OrganizationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationContactses");

            migrationBuilder.DropColumn(
                name: "UsgMechanism",
                table: "Organizations");
        }
    }
}
