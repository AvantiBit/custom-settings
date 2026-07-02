using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avantibit.Optimizely.CustomSettings.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingsType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    JsonData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "Settings_Composite",
                table: "CustomSettings",
                columns: new[] { "SettingsType", "SiteId", "LanguageCode" },
                unique: true,
                filter: "[SiteId] IS NOT NULL AND [LanguageCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomSettings");
        }
    }
}
