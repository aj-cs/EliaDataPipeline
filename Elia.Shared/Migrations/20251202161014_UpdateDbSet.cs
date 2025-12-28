using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elia.Shared.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDbSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Forecast_RawData_RawDataId",
                table: "Forecast");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Forecast",
                table: "Forecast");

            migrationBuilder.RenameTable(
                name: "Forecast",
                newName: "Forecasts");

            migrationBuilder.RenameIndex(
                name: "IX_Forecast_RawDataId",
                table: "Forecasts",
                newName: "IX_Forecasts_RawDataId");

            migrationBuilder.RenameIndex(
                name: "IX_Forecast_DatasetId_Region_ValidTime_VersionTime",
                table: "Forecasts",
                newName: "IX_Forecasts_DatasetId_Region_ValidTime_VersionTime");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Forecasts",
                table: "Forecasts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Forecasts_RawData_RawDataId",
                table: "Forecasts",
                column: "RawDataId",
                principalTable: "RawData",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Forecasts_RawData_RawDataId",
                table: "Forecasts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Forecasts",
                table: "Forecasts");

            migrationBuilder.RenameTable(
                name: "Forecasts",
                newName: "Forecast");

            migrationBuilder.RenameIndex(
                name: "IX_Forecasts_RawDataId",
                table: "Forecast",
                newName: "IX_Forecast_RawDataId");

            migrationBuilder.RenameIndex(
                name: "IX_Forecasts_DatasetId_Region_ValidTime_VersionTime",
                table: "Forecast",
                newName: "IX_Forecast_DatasetId_Region_ValidTime_VersionTime");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Forecast",
                table: "Forecast",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Forecast_RawData_RawDataId",
                table: "Forecast",
                column: "RawDataId",
                principalTable: "RawData",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
