using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticEMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingModesToRecipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CombinedDenominatorIndices",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CombinedNumeratorIndices",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DualSubMode",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CombinedDenominatorIndices",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "CombinedNumeratorIndices",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "DualSubMode",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "MultiSubMode",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ProcessingMode",
                table: "Recipes");
        }
    }
}
