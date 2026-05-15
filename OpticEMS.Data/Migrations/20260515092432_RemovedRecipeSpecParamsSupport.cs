using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticEMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovedRecipeSpecParamsSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExposureMs",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ScansNum",
                table: "Recipes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "ExposureMs",
                table: "Recipes",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "ScansNum",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
