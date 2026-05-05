using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticEMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMagneticSupportToEndpoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FieldPeriodsToAverage",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MagneticFieldPeriod",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FieldPeriodsToAverage",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "MagneticFieldPeriod",
                table: "Recipes");
        }
    }
}
