using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elia.Shared.Migrations
{
    /// <inheritdoc />
    public partial class UpdateForecast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Forecast_ValidTime_VersionTime",
                table: "Forecast");

            migrationBuilder.AddColumn<string>(
                name: "DatasetId",
                table: "Forecast",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Horizon",
                table: "Forecast",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RawDataId",
                table: "Forecast",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Forecast",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Forecast_DatasetId_Region_ValidTime_VersionTime",
                table: "Forecast",
                columns: new[] { "DatasetId", "Region", "ValidTime", "VersionTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Forecast_RawDataId",
                table: "Forecast",
                column: "RawDataId");

            migrationBuilder.AddForeignKey(
                name: "FK_Forecast_RawData_RawDataId",
                table: "Forecast",
                column: "RawDataId",
                principalTable: "RawData",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Forecast_RawData_RawDataId",
                table: "Forecast");

            migrationBuilder.DropIndex(
                name: "IX_Forecast_DatasetId_Region_ValidTime_VersionTime",
                table: "Forecast");

            migrationBuilder.DropIndex(
                name: "IX_Forecast_RawDataId",
                table: "Forecast");

            migrationBuilder.DropColumn(
                name: "DatasetId",
                table: "Forecast");

            migrationBuilder.DropColumn(
                name: "Horizon",
                table: "Forecast");

            migrationBuilder.DropColumn(
                name: "RawDataId",
                table: "Forecast");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Forecast");

            migrationBuilder.CreateIndex(
                name: "IX_Forecast_ValidTime_VersionTime",
                table: "Forecast",
                columns: new[] { "ValidTime", "VersionTime" },
                unique: true);
        }
    }
}
