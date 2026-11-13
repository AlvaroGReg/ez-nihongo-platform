using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNihongo.Platform.Api.Migrations
{
    /// <inheritdoc />
    public partial class DailyRegistrationLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyRegistrationUsage",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRegistrationUsage", x => x.Date);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyRegistrationUsage");
        }
    }
}
