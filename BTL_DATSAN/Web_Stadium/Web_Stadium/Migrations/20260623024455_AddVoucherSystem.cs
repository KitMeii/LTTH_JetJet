using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Stadium.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DaDung",
                table: "Vouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DieuKienToiThieu",
                table: "Vouchers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "LoaiVoucher",
                table: "Vouchers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "HeThong");

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayBatDau",
                table: "Vouchers",
                type: "datetime",
                nullable: false,
                defaultValueSql: "(getdate())");

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayHetHan",
                table: "Vouchers",
                type: "datetime",
                nullable: false,
                defaultValueSql: "(getdate())");

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
                name: "SoLuong",
                table: "Vouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TienGiamHeThong",
                table: "DatSans",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TienGiamSan",
                table: "DatSans",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TienGoc",
                table: "DatSans",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VoucherHeThongId",
                table: "DatSans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VoucherSanId",
                table: "DatSans",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_LoaiVoucher",
                table: "Vouchers",
                column: "LoaiVoucher");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_OwnerId",
                table: "Vouchers",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_SanBongId",
                table: "Vouchers",
                column: "SanBongId");

            migrationBuilder.CreateIndex(
                name: "IX_DatSans_VoucherHeThongId",
                table: "DatSans",
                column: "VoucherHeThongId");

            migrationBuilder.CreateIndex(
                name: "IX_DatSans_VoucherSanId",
                table: "DatSans",
                column: "VoucherSanId");

            migrationBuilder.AddForeignKey(
                name: "FK_DatSans_VoucherHeThong",
                table: "DatSans",
                column: "VoucherHeThongId",
                principalTable: "Vouchers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DatSans_VoucherSan",
                table: "DatSans",
                column: "VoucherSanId",
                principalTable: "Vouchers",
                principalColumn: "Id");

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
                name: "FK_DatSans_VoucherHeThong",
                table: "DatSans");

            migrationBuilder.DropForeignKey(
                name: "FK_DatSans_VoucherSan",
                table: "DatSans");

            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Owner",
                table: "Vouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_SanBong",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_LoaiVoucher",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_OwnerId",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_SanBongId",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_DatSans_VoucherHeThongId",
                table: "DatSans");

            migrationBuilder.DropIndex(
                name: "IX_DatSans_VoucherSanId",
                table: "DatSans");

            migrationBuilder.DropColumn(
                name: "DaDung",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "DieuKienToiThieu",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "LoaiVoucher",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "NgayBatDau",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "NgayHetHan",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "SanBongId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "SoLuong",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "TienGiamHeThong",
                table: "DatSans");

            migrationBuilder.DropColumn(
                name: "TienGiamSan",
                table: "DatSans");

            migrationBuilder.DropColumn(
                name: "TienGoc",
                table: "DatSans");

            migrationBuilder.DropColumn(
                name: "VoucherHeThongId",
                table: "DatSans");

            migrationBuilder.DropColumn(
                name: "VoucherSanId",
                table: "DatSans");
        }
    }
}
