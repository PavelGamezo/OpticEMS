using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticEMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovedIonizationFromSpectralLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropColumn(
                name: "Ionization",
                table: "SpectralLines");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ionization",
                table: "SpectralLines",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AutocalibrationEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DerivativeEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DerivativePoints = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectionWindowHighs = table.Column<string>(type: "TEXT", nullable: false),
                    DetectionWindowTimes = table.Column<string>(type: "TEXT", nullable: false),
                    DualSubMode = table.Column<int>(type: "INTEGER", nullable: false),
                    FieldPeriodsToAverage = table.Column<int>(type: "INTEGER", nullable: false),
                    InitialDelay = table.Column<int>(type: "INTEGER", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MagneticFieldPeriodMs = table.Column<float>(type: "REAL", nullable: false),
                    MaxEndpointTime = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    OverEtchEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    OverEtchValue = table.Column<int>(type: "INTEGER", nullable: false),
                    PcaComponents = table.Column<int>(type: "INTEGER", nullable: false),
                    PcaEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PcaMinTrainingSize = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessingMode = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    WavelengthColors = table.Column<string>(type: "TEXT", nullable: false),
                    WavelengthNames = table.Column<string>(type: "TEXT", nullable: false),
                    Wavelengths = table.Column<string>(type: "TEXT", nullable: false),
                    WindowInCounts = table.Column<string>(type: "TEXT", nullable: false),
                    WindowOutCounts = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.DatabaseId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_RecipeId",
                table: "Recipes",
                column: "RecipeId");
        }
    }
}
