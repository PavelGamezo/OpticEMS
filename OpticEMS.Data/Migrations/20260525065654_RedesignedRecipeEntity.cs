using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticEMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RedesignedRecipeEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CombinedDenominatorIndices",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "CombinedExpression",
                table: "Recipes");

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
                name: "ExposureMs",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "FieldPeriodsToAverage",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "MagneticFieldPeriodMs",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "MultiSubMode",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ProcessingMode",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ScansNum",
                table: "Recipes");

            migrationBuilder.RenameColumn(
                name: "CombinedNumeratorIndices",
                table: "Recipes",
                newName: "GraphJson");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GraphJson",
                table: "Recipes",
                newName: "CombinedNumeratorIndices");

            migrationBuilder.AddColumn<string>(
                name: "CombinedDenominatorIndices",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CombinedExpression",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.AddColumn<float>(
                name: "ExposureMs",
                table: "Recipes",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

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
                name: "MultiSubMode",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingMode",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ScansNum",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
