using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticEMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImplementedRecipeEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Wavelengths = table.Column<string>(type: "TEXT", nullable: false),
                    WavelengthColors = table.Column<string>(type: "TEXT", nullable: false),
                    InitialDelay = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectionWindowHighs = table.Column<string>(type: "TEXT", nullable: false),
                    DetectionWindowTime = table.Column<int>(type: "INTEGER", nullable: false),
                    ExposureMs = table.Column<float>(type: "REAL", nullable: false),
                    ScansNum = table.Column<int>(type: "INTEGER", nullable: false),
                    WindowInCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WindowOutCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StableThresholdPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxEndpointTime = table.Column<int>(type: "INTEGER", nullable: false),
                    AutocalibrationEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    OverEtchEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    OverEtchValue = table.Column<int>(type: "INTEGER", nullable: false),
                    PcaEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PcaComponents = table.Column<int>(type: "INTEGER", nullable: false),
                    PcaMinTrainingSize = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Recipes");
        }
    }
}
