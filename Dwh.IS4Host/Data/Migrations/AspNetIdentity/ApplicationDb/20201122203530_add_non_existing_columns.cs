using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dwh.IS4Host.Data.Migrations.AspNetIdentity.ApplicationDb
{
    public partial class add_non_existing_columns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "Users",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockoutEnd",
                table: "Users",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                maxLength: 256,
                table: "Users",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedUserName",
                maxLength: 256,
                table: "Users",
                nullable: true);


            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
