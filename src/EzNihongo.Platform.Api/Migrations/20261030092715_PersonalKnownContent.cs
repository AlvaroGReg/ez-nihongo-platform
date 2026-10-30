using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzNihongo.Platform.Api.Migrations
{
    /// <inheritdoc />
    public partial class PersonalKnownContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserKnownContent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CatalogContentId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Word = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reading = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Romaji = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Meaning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Kanji = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Readings = table.Column<string[]>(type: "text[]", nullable: true),
                    Meanings = table.Column<string[]>(type: "text[]", nullable: true),
                    NormalizedWriting = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    NormalizedReading = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserKnownContent", x => x.Id);
                    table.CheckConstraint("CK_UserKnownContent_SourceShape", "(\"Source\" = 'catalog' AND \"CatalogContentId\" IS NOT NULL AND \"NormalizedWriting\" IS NULL AND \"NormalizedReading\" IS NULL AND \"Word\" IS NULL AND \"Reading\" IS NULL AND \"Romaji\" IS NULL AND \"Meaning\" IS NULL AND \"Kanji\" IS NULL AND \"Readings\" IS NULL AND \"Meanings\" IS NULL) OR (\"Source\" = 'custom' AND \"CatalogContentId\" IS NULL AND \"NormalizedWriting\" IS NOT NULL AND ((\"ContentType\" = 'vocabulary' AND \"NormalizedReading\" IS NOT NULL AND \"Word\" IS NOT NULL AND \"Reading\" IS NOT NULL AND \"Romaji\" IS NOT NULL AND \"Meaning\" IS NOT NULL AND \"Kanji\" IS NULL AND \"Readings\" IS NULL AND \"Meanings\" IS NULL) OR (\"ContentType\" = 'kanji' AND \"NormalizedReading\" IS NULL AND \"Kanji\" IS NOT NULL AND \"Readings\" IS NOT NULL AND \"Meanings\" IS NOT NULL AND \"Word\" IS NULL AND \"Reading\" IS NULL AND \"Romaji\" IS NULL AND \"Meaning\" IS NULL)))");
                    table.ForeignKey(
                        name: "FK_UserKnownContent_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserKnownContent_UserId_ContentType_CatalogContentId",
                table: "UserKnownContent",
                columns: new[] { "UserId", "ContentType", "CatalogContentId" },
                unique: true,
                filter: "\"Source\" = 'catalog'");

            migrationBuilder.CreateIndex(
                name: "IX_UserKnownContent_UserId_ContentType_CreatedAt_Id",
                table: "UserKnownContent",
                columns: new[] { "UserId", "ContentType", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_UserKnownContent_UserId_ContentType_NormalizedWriting",
                table: "UserKnownContent",
                columns: new[] { "UserId", "ContentType", "NormalizedWriting" },
                unique: true,
                filter: "\"Source\" = 'custom' AND \"ContentType\" = 'kanji'");

            migrationBuilder.CreateIndex(
                name: "IX_UserKnownContent_UserId_ContentType_NormalizedWriting_Norma~",
                table: "UserKnownContent",
                columns: new[] { "UserId", "ContentType", "NormalizedWriting", "NormalizedReading" },
                unique: true,
                filter: "\"Source\" = 'custom' AND \"ContentType\" = 'vocabulary'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserKnownContent");
        }
    }
}
