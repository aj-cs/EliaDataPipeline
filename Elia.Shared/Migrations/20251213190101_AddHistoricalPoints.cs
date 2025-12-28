using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elia.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoricalPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DatasetId = table.Column<string>(type: "text", nullable: false),
                    EnergyType = table.Column<string>(type: "text", nullable: false),
                    Region = table.Column<string>(type: "text", nullable: false),
                    ValidTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MeasuredMW = table.Column<double>(type: "double precision", nullable: false),
                    OffshoreOnshore = table.Column<string>(type: "text", nullable: true),
                    GridConnectionType = table.Column<string>(type: "text", nullable: true),
                    RawDataId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalPoints_RawData_RawDataId",
                        column: x => x.RawDataId,
                        principalTable: "RawData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPoints_DatasetId_EnergyType_Region_ValidTime_Offs~",
                table: "HistoricalPoints",
                columns: new[] { "DatasetId", "EnergyType", "Region", "ValidTime", "OffshoreOnshore", "GridConnectionType" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPoints_RawDataId",
                table: "HistoricalPoints",
                column: "RawDataId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalPoints");
        }
    }
}
