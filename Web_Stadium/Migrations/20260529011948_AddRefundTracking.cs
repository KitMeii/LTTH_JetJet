using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Stadium.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LoaiHoanCoc",
                table: "DatSans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NguonHuy",
                table: "DatSans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PhanTramHoan",
                table: "DatSans",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SoTienDaHoan",
                table: "DatSans",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GiaoDichHoanCocs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatSanId = table.Column<int>(type: "int", nullable: false),
                    ThoiGianGiaoDich = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VaiTroNguoiKhoiTao = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NguoiKhoiTaoId = table.Column<int>(type: "int", nullable: true),
                    TrangThaiHoan = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__GiaoDich__3214EC07A1B2C3D4", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiaoDichHoanCoc_DatSan",
                        column: x => x.DatSanId,
                        principalTable: "DatSans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GiaoDichHoanCoc_User",
                        column: x => x.NguoiKhoiTaoId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichHoanCoc_DatSanId",
                table: "GiaoDichHoanCocs",
                column: "DatSanId");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichHoanCoc_ThoiGian",
                table: "GiaoDichHoanCocs",
                column: "ThoiGianGiaoDich");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichHoanCoc_VaiTro",
                table: "GiaoDichHoanCocs",
                column: "VaiTroNguoiKhoiTao");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichHoanCocs_NguoiKhoiTaoId",
                table: "GiaoDichHoanCocs",
                column: "NguoiKhoiTaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GiaoDichHoanCocs");

            migrationBuilder.DropColumn(
                name: "LoaiHoanCoc",
                table: "DatSans");

            migrationBuilder.DropColumn(
                name: "NguonHuy",
                table: "DatSans");

            migrationBuilder.DropColumn(
                name: "PhanTramHoan",
                table: "DatSans");

            migrationBuilder.DropColumn(
                name: "SoTienDaHoan",
                table: "DatSans");
        }
    }
}
