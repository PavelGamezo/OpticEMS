using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticEMS.Data.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpectralLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Element = table.Column<string>(type: "TEXT", nullable: false),
                    Wavelength = table.Column<double>(type: "REAL", nullable: false),
                    Ionization = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpectralLines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpectralLines_Wavelength",
                table: "SpectralLines",
                column: "Wavelength");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpectralLines");
        }
    }
}
