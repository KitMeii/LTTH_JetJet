using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Stadium.Migrations
{
    /// <inheritdoc />
    public partial class FixDateTimeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LyDoHuy",
                table: "SanBongs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PhanTramHoanCocDungHan",
                table: "SanBongs",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PhanTramHoanCocTreHan",
                table: "SanBongs",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ThoiGianGiuCho",
                table: "SanBongs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ThoiGianHuyTruocGioDa",
                table: "SanBongs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayPhanHoi",
                table: "DanhGias",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhanHoiOwner",
                table: "DanhGias",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnhSanBongs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SanBongId = table.Column<int>(type: "int", nullable: false),
                    DuongDan = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LoaiAnh = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Upload"),
                    ThuTu = table.Column<int>(type: "int", nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NgayThem = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnhSanBongs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnhSanBongs_SanBong",
                        column: x => x.SanBongId,
                        principalTable: "SanBongs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnhSanBongs_SanBong",
                table: "AnhSanBongs",
                columns: new[] { "SanBongId", "ThuTu" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnhSanBongs");

            migrationBuilder.DropColumn(
                name: "LyDoHuy",
                table: "SanBongs");

            migrationBuilder.DropColumn(
                name: "PhanTramHoanCocDungHan",
                table: "SanBongs");

            migrationBuilder.DropColumn(
                name: "PhanTramHoanCocTreHan",
                table: "SanBongs");

            migrationBuilder.DropColumn(
                name: "ThoiGianGiuCho",
                table: "SanBongs");

            migrationBuilder.DropColumn(
                name: "ThoiGianHuyTruocGioDa",
                table: "SanBongs");

            migrationBuilder.DropColumn(
                name: "NgayPhanHoi",
                table: "DanhGias");

            migrationBuilder.DropColumn(
                name: "PhanHoiOwner",
                table: "DanhGias");
        }
    }
}
