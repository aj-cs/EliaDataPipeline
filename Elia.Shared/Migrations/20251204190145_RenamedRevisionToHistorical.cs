using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elia.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RenamedRevisionToHistorical : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsRevision",
                table: "Forecasts",
                newName: "IsHistoricalVersion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsHistoricalVersion",
                table: "Forecasts",
                newName: "IsRevision");
        }
    }
}
