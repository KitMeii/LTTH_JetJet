using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Stadium.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerVoucherAndYeuCauTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LoaiPhatHanh",
                table: "Vouchers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "HeThong");

            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "Vouchers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SanBongId",
                table: "Vouchers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoLuotConLai",
                table: "Vouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChuyenNhuongDatSans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatSanId = table.Column<int>(type: "int", nullable: false),
                    NguoiChuyenId = table.Column<int>(type: "int", nullable: false),
                    EmailNguoiNhan = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SdtNguoiNhan = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NguoiNhanId = table.Column<int>(type: "int", nullable: true),
                    LyDo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "ChoPheDuyet"),
                    NgayTao = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    NgayXuLy = table.Column<DateTime>(type: "datetime", nullable: true),
                    NguoiXuLyOwnerId = table.Column<int>(type: "int", nullable: true),
                    GhiChuXuLy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChuyenNhuongDatSans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CNDS_DatSan",
                        column: x => x.DatSanId,
                        principalTable: "DatSans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CNDS_NguoiChuyen",
                        column: x => x.NguoiChuyenId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CNDS_NguoiNhan",
                        column: x => x.NguoiNhanId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CNDS_OwnerXuLy",
                        column: x => x.NguoiXuLyOwnerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "YeuCauDoiGios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatSanId = table.Column<int>(type: "int", nullable: false),
                    KhungGioMoiId = table.Column<int>(type: "int", nullable: false),
                    NgayThiDauMoi = table.Column<DateTime>(type: "datetime", nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "ChoPheDuyet"),
                    NgayTao = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    NgayXuLy = table.Column<DateTime>(type: "datetime", nullable: true),
                    NguoiXuLyId = table.Column<int>(type: "int", nullable: true),
                    GhiChuXuLy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauDoiGios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YCDG_DatSan",
                        column: x => x.DatSanId,
                        principalTable: "DatSans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_YCDG_KhungGio",
                        column: x => x.KhungGioMoiId,
                        principalTable: "KhungGios",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_YCDG_NguoiXuLy",
                        column: x => x.NguoiXuLyId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "YeuCauDoiSans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatSanId = table.Column<int>(type: "int", nullable: false),
                    KhungGioMoiId = table.Column<int>(type: "int", nullable: false),
                    NgayThiDauMoi = table.Column<DateTime>(type: "datetime", nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "ChoPheDuyet"),
                    NgayTao = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    NgayXuLy = table.Column<DateTime>(type: "datetime", nullable: true),
                    NguoiXuLyId = table.Column<int>(type: "int", nullable: true),
                    GhiChuXuLy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauDoiSans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YCDS_DatSan",
                        column: x => x.DatSanId,
                        principalTable: "DatSans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_YCDS_KhungGio",
                        column: x => x.KhungGioMoiId,
                        principalTable: "KhungGios",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_YCDS_NguoiXuLy",
                        column: x => x.NguoiXuLyId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_OwnerId",
                table: "Vouchers",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_SanBongId",
                table: "Vouchers",
                column: "SanBongId");

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenNhuongDatSans_NguoiChuyenId",
                table: "ChuyenNhuongDatSans",
                column: "NguoiChuyenId");

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenNhuongDatSans_NguoiNhanId",
                table: "ChuyenNhuongDatSans",
                column: "NguoiNhanId");

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenNhuongDatSans_NguoiXuLyOwnerId",
                table: "ChuyenNhuongDatSans",
                column: "NguoiXuLyOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CNDS_DatSanId",
                table: "ChuyenNhuongDatSans",
                column: "DatSanId");

            migrationBuilder.CreateIndex(
                name: "IX_CNDS_TrangThai",
                table: "ChuyenNhuongDatSans",
                column: "TrangThai");

            migrationBuilder.CreateIndex(
                name: "IX_YCDG_DatSanId",
                table: "YeuCauDoiGios",
                column: "DatSanId");

            migrationBuilder.CreateIndex(
                name: "IX_YCDG_TrangThai",
                table: "YeuCauDoiGios",
                column: "TrangThai");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauDoiGios_KhungGioMoiId",
                table: "YeuCauDoiGios",
                column: "KhungGioMoiId");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauDoiGios_NguoiXuLyId",
                table: "YeuCauDoiGios",
                column: "NguoiXuLyId");

            migrationBuilder.CreateIndex(
                name: "IX_YCDS_DatSanId",
                table: "YeuCauDoiSans",
                column: "DatSanId");

            migrationBuilder.CreateIndex(
                name: "IX_YCDS_TrangThai",
                table: "YeuCauDoiSans",
                column: "TrangThai");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauDoiSans_KhungGioMoiId",
                table: "YeuCauDoiSans",
                column: "KhungGioMoiId");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauDoiSans_NguoiXuLyId",
                table: "YeuCauDoiSans",
                column: "NguoiXuLyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_Owner",
                table: "Vouchers",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_SanBong",
                table: "Vouchers",
                column: "SanBongId",
                principalTable: "SanBongs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Owner",
                table: "Vouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_SanBong",
                table: "Vouchers");

            migrationBuilder.DropTable(
                name: "ChuyenNhuongDatSans");

            migrationBuilder.DropTable(
                name: "YeuCauDoiGios");

            migrationBuilder.DropTable(
                name: "YeuCauDoiSans");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_OwnerId",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_SanBongId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "LoaiPhatHanh",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "SanBongId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "SoLuotConLai",
                table: "Vouchers");
        }
    }
}
