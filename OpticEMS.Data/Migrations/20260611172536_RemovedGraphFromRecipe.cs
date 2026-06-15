using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticEMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovedGraphFromRecipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GraphJson",
                table: "Recipes");

            migrationBuilder.AddColumn<bool>(
                name: "DerivativeEnabled",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DerivativePoints",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DualSubMode",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FieldPeriodsToAverage",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "MagneticFieldPeriodMs",
                table: "Recipes",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingMode",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DerivativeEnabled",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "DerivativePoints",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "DualSubMode",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "FieldPeriodsToAverage",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "MagneticFieldPeriodMs",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ProcessingMode",
                table: "Recipes");

            migrationBuilder.AddColumn<string>(
                name: "GraphJson",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
