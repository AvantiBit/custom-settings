using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avantibit.Optimizely.CustomSettings.Migrations
{
    /// <summary>
    /// Adds three filtered unique indexes to cover the null/non-null combinations of
    /// <c>SiteId</c> and <c>LanguageCode</c> that the original <c>Settings_Composite</c> index
    /// intentionally excluded via its filter.
    ///
    /// SQL Server unique indexes silently ignore rows that contain NULL in any indexed column,
    /// so the original single filtered index only enforced uniqueness when both columns were
    /// non-null. This left global settings (SiteId = NULL), language-neutral settings
    /// (LanguageCode = NULL), and global language-neutral settings (both NULL) unprotected
    /// against duplicate rows from concurrent inserts.
    ///
    /// The four indexes together cover every scope combination:
    ///   Settings_Composite          – SiteId IS NOT NULL AND LanguageCode IS NOT NULL (existing)
    ///   Settings_Composite_NullSite – SiteId IS NULL     AND LanguageCode IS NOT NULL (new)
    ///   Settings_Composite_NullLang – SiteId IS NOT NULL AND LanguageCode IS NULL     (new)
    ///   Settings_Composite_NullBoth – SiteId IS NULL     AND LanguageCode IS NULL     (new)
    /// </summary>
    public partial class FixNullableUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove duplicates before creating each unique index so the migration
            // succeeds on databases that already contain duplicate rows caused by the
            // original unfiltered-null index bug. For each null-scope we keep only the
            // row with the most recent ModifiedUtc (highest Id on a tie).

            // Scope: SiteId IS NULL, LanguageCode IS NOT NULL
            migrationBuilder.Sql(@"
WITH CTE AS (
    SELECT Id,
           ROW_NUMBER() OVER (
               PARTITION BY SettingsType, LanguageCode
               ORDER BY ModifiedUtc DESC, Id DESC
           ) AS rn
    FROM CustomSettings
    WHERE SiteId IS NULL AND LanguageCode IS NOT NULL
)
DELETE FROM CTE WHERE rn > 1;");

            migrationBuilder.CreateIndex(
                name: "Settings_Composite_NullSite",
                table: "CustomSettings",
                columns: new[] { "SettingsType", "LanguageCode" },
                unique: true,
                filter: "[SiteId] IS NULL AND [LanguageCode] IS NOT NULL");

            // Scope: SiteId IS NOT NULL, LanguageCode IS NULL
            migrationBuilder.Sql(@"
WITH CTE AS (
    SELECT Id,
           ROW_NUMBER() OVER (
               PARTITION BY SettingsType, SiteId
               ORDER BY ModifiedUtc DESC, Id DESC
           ) AS rn
    FROM CustomSettings
    WHERE SiteId IS NOT NULL AND LanguageCode IS NULL
)
DELETE FROM CTE WHERE rn > 1;");

            migrationBuilder.CreateIndex(
                name: "Settings_Composite_NullLang",
                table: "CustomSettings",
                columns: new[] { "SettingsType", "SiteId" },
                unique: true,
                filter: "[SiteId] IS NOT NULL AND [LanguageCode] IS NULL");

            // Scope: SiteId IS NULL, LanguageCode IS NULL (global/language-neutral)
            migrationBuilder.Sql(@"
WITH CTE AS (
    SELECT Id,
           ROW_NUMBER() OVER (
               PARTITION BY SettingsType
               ORDER BY ModifiedUtc DESC, Id DESC
           ) AS rn
    FROM CustomSettings
    WHERE SiteId IS NULL AND LanguageCode IS NULL
)
DELETE FROM CTE WHERE rn > 1;");

            migrationBuilder.CreateIndex(
                name: "Settings_Composite_NullBoth",
                table: "CustomSettings",
                columns: new[] { "SettingsType" },
                unique: true,
                filter: "[SiteId] IS NULL AND [LanguageCode] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "Settings_Composite_NullSite",
                table: "CustomSettings");

            migrationBuilder.DropIndex(
                name: "Settings_Composite_NullLang",
                table: "CustomSettings");

            migrationBuilder.DropIndex(
                name: "Settings_Composite_NullBoth",
                table: "CustomSettings");
        }
    }
}
