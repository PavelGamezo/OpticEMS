using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticEMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddedDerivativeSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StableThresholdPercent",
                table: "Recipes",
                newName: "DerivativePoints");

            migrationBuilder.RenameColumn(
                name: "MagneticFieldPeriod",
                table: "Recipes",
                newName: "DerivativeEnabled");

            migrationBuilder.AddColumn<float>(
                name: "MagneticFieldPeriodMs",
                table: "Recipes",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MagneticFieldPeriodMs",
                table: "Recipes");

            migrationBuilder.RenameColumn(
                name: "DerivativePoints",
                table: "Recipes",
                newName: "StableThresholdPercent");

            migrationBuilder.RenameColumn(
                name: "DerivativeEnabled",
                table: "Recipes",
                newName: "MagneticFieldPeriod");
        }
    }
}
