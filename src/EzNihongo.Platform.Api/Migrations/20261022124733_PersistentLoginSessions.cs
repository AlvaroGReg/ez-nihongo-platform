using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNihongo.Platform.Api.Migrations
{
    /// <inheritdoc />
    public partial class PersistentLoginSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersistentLoginSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RotatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersistentLoginSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersistentLoginSessions_PersistentLoginSessions_ReplacedById",
                        column: x => x.ReplacedById,
                        principalTable: "PersistentLoginSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PersistentLoginSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersistentLoginSessions_FamilyId",
                table: "PersistentLoginSessions",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_PersistentLoginSessions_ReplacedById",
                table: "PersistentLoginSessions",
                column: "ReplacedById");

            migrationBuilder.CreateIndex(
                name: "IX_PersistentLoginSessions_TokenHash",
                table: "PersistentLoginSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersistentLoginSessions_UserId",
                table: "PersistentLoginSessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersistentLoginSessions");
        }
    }
}
