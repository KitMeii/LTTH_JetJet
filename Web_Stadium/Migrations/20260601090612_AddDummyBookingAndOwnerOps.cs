using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Stadium.Migrations
{
    /// <inheritdoc />
    public partial class AddDummyBookingAndOwnerOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LichBlockJson",
                table: "GiaiDaus",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StaffPhuTrachId",
                table: "GiaiDaus",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GiaiDauId",
                table: "DatSans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LaDummyBooking",
                table: "DatSans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_GiaiDaus_StaffPhuTrachId",
                table: "GiaiDaus",
                column: "StaffPhuTrachId");

            migrationBuilder.CreateIndex(
                name: "IX_DatSans_GiaiDauId",
                table: "DatSans",
                column: "GiaiDauId");

            migrationBuilder.AddForeignKey(
                name: "FK_DatSans_GiaiDaus_GiaiDauId",
                table: "DatSans",
                column: "GiaiDauId",
                principalTable: "GiaiDaus",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GiaiDaus_Users_StaffPhuTrachId",
                table: "GiaiDaus",
                column: "StaffPhuTrachId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DatSans_GiaiDaus_GiaiDauId",
                table: "DatSans");

            migrationBuilder.DropForeignKey(
                name: "FK_GiaiDaus_Users_StaffPhuTrachId",
                table: "GiaiDaus");

            migrationBuilder.DropIndex(
                name: "IX_GiaiDaus_StaffPhuTrachId",
                table: "GiaiDaus");

            migrationBuilder.DropIndex(
                name: "IX_DatSans_GiaiDauId",
                table: "DatSans");

            migrationBuilder.DropColumn(
                name: "LichBlockJson",
                table: "GiaiDaus");

            migrationBuilder.DropColumn(
                name: "StaffPhuTrachId",
                table: "GiaiDaus");

            migrationBuilder.DropColumn(
                name: "GiaiDauId",
                table: "DatSans");

            migrationBuilder.DropColumn(
                name: "LaDummyBooking",
                table: "DatSans");
        }
    }
}
