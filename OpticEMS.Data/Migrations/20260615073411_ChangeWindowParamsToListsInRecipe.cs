using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticEMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeWindowParamsToListsInRecipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetectionWindowTime",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "WindowInCount",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "WindowOutCount",
                table: "Recipes");

            migrationBuilder.AddColumn<string>(
                name: "DetectionWindowTimes",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WindowInCounts",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WindowOutCounts",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetectionWindowTimes",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "WindowInCounts",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "WindowOutCounts",
                table: "Recipes");

            migrationBuilder.AddColumn<int>(
                name: "DetectionWindowTime",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WindowInCount",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WindowOutCount",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
