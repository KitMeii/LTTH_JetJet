using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Stadium.Migrations
{
    /// <inheritdoc />
    [Migration("20260703103000_AddAutoModeGiaiDau")]
    public partial class AddAutoModeGiaiDau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoMode",
                table: "GiaiDaus",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoMode",
                table: "GiaiDaus");
        }
    }
}
