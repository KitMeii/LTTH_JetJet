using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Stadium.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DaXacThucSdt",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DiemHienTai",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NganHang",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoTaiKhoan",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenTaiKhoan",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoSaoCoSoVatChat",
                table: "DanhGias",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoSaoNhanVien",
                table: "DanhGias",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiemThuongLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SoDiem = table.Column<int>(type: "int", nullable: false),
                    SoDuSauGD = table.Column<int>(type: "int", nullable: false),
                    LoaiSuKien = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DatSanId = table.Column<int>(type: "int", nullable: true),
                    ThoiGian = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__DiemThuo__3214EC07082AC8BB", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiemLog_DatSan",
                        column: x => x.DatSanId,
                        principalTable: "DatSans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DiemLog_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GiaiDaus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenGiai = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SanBongId = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    SoDoiToiDa = table.Column<int>(type: "int", nullable: false),
                    SoBang = table.Column<int>(type: "int", nullable: false),
                    LePhiGiai = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TienKyQuy = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TienPhatTheVang = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TienPhatTheDo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SoTranTreoGioTheDo = table.Column<int>(type: "int", nullable: false),
                    SoTheVangTichLuy = table.Column<int>(type: "int", nullable: false),
                    NgayBatDau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayKetThuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ThoiGianTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ThoiGianDongDanhSach = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiaiDaus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiaiDaus_SanBongs_SanBongId",
                        column: x => x.SanBongId,
                        principalTable: "SanBongs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GiaiDaus_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OtpCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SoDienThoai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaOtp = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    NgayHetHan = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(dateadd(minute,(5),getdate()))"),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__OtpCodes__3214EC072CAD03AF", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtpCodes_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SanYeuThichs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SanBongId = table.Column<int>(type: "int", nullable: false),
                    NgayThem = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SanYeuTh__3214EC07643721CD", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SanYeuThich_SanBong",
                        column: x => x.SanBongId,
                        principalTable: "SanBongs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SanYeuThich_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaVoucher = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenVoucher = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LoaiGiam = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "PhanTram"),
                    GiaTriGiam = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GiamToiDa = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DiemCanDoi = table.Column<int>(type: "int", nullable: false),
                    SoNgayHieuLuc = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    NgayTao = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Vouchers__3214EC07092807B4", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BangDaus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GiaiDauId = table.Column<int>(type: "int", nullable: false),
                    TenBang = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BangDaus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BangDaus_GiaiDaus_GiaiDauId",
                        column: x => x.GiaiDauId,
                        principalTable: "GiaiDaus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserVouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: false),
                    MaSuDung = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NgayDoi = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    NgayHetHan = table.Column<DateTime>(type: "datetime", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    NgaySuDung = table.Column<DateTime>(type: "datetime", nullable: true),
                    DatSanId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__UserVouc__3214EC0796E42189", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserVoucher_DatSan",
                        column: x => x.DatSanId,
                        principalTable: "DatSans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserVoucher_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserVoucher_Voucher",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DoiBongs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GiaiDauId = table.Column<int>(type: "int", nullable: false),
                    BangId = table.Column<int>(type: "int", nullable: true),
                    DoiTruongId = table.Column<int>(type: "int", nullable: false),
                    TenDoi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TienKyQuyConLai = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DaThanhToan = table.Column<bool>(type: "bit", nullable: false),
                    ThoiGianThanhToan = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ThoiGianTao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoiBongs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoiBongs_BangDaus_BangId",
                        column: x => x.BangId,
                        principalTable: "BangDaus",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DoiBongs_GiaiDaus_GiaiDauId",
                        column: x => x.GiaiDauId,
                        principalTable: "GiaiDaus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DoiBongs_Users_DoiTruongId",
                        column: x => x.DoiTruongId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ThanhVienDois",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoiId = table.Column<int>(type: "int", nullable: false),
                    HoTen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SoAo = table.Column<int>(type: "int", nullable: false),
                    AnhDaiDien = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SoTranTreoGio = table.Column<int>(type: "int", nullable: false),
                    TongBanThang = table.Column<int>(type: "int", nullable: false),
                    TongTheVang = table.Column<int>(type: "int", nullable: false),
                    TongTheDo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThanhVienDois", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThanhVienDois_DoiBongs_DoiId",
                        column: x => x.DoiId,
                        principalTable: "DoiBongs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TranDaus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GiaiDauId = table.Column<int>(type: "int", nullable: false),
                    BangId = table.Column<int>(type: "int", nullable: true),
                    KhungGioId = table.Column<int>(type: "int", nullable: true),
                    DoiNhaId = table.Column<int>(type: "int", nullable: false),
                    DoiKhachId = table.Column<int>(type: "int", nullable: false),
                    BanThangNha = table.Column<int>(type: "int", nullable: true),
                    BanThangKhach = table.Column<int>(type: "int", nullable: true),
                    VongDau = table.Column<int>(type: "int", nullable: false),
                    LoaiVong = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NgayThiDau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StaffPhuTrachId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranDaus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranDaus_BangDaus_BangId",
                        column: x => x.BangId,
                        principalTable: "BangDaus",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TranDaus_DoiBongs_DoiKhachId",
                        column: x => x.DoiKhachId,
                        principalTable: "DoiBongs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TranDaus_DoiBongs_DoiNhaId",
                        column: x => x.DoiNhaId,
                        principalTable: "DoiBongs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TranDaus_GiaiDaus_GiaiDauId",
                        column: x => x.GiaiDauId,
                        principalTable: "GiaiDaus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TranDaus_KhungGios_KhungGioId",
                        column: x => x.KhungGioId,
                        principalTable: "KhungGios",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TranDaus_Users_StaffPhuTrachId",
                        column: x => x.StaffPhuTrachId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SuKienTrans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TranDauId = table.Column<int>(type: "int", nullable: false),
                    ThanhVienId = table.Column<int>(type: "int", nullable: true),
                    DoiId = table.Column<int>(type: "int", nullable: true),
                    LoaiSuKien = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phut = table.Column<int>(type: "int", nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThoiGianGhi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DoiBongId = table.Column<int>(type: "int", nullable: true),
                    ThanhVienDoiId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuKienTrans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuKienTrans_DoiBongs_DoiBongId",
                        column: x => x.DoiBongId,
                        principalTable: "DoiBongs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SuKienTrans_DoiBongs_DoiId",
                        column: x => x.DoiId,
                        principalTable: "DoiBongs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SuKienTrans_ThanhVienDois_ThanhVienDoiId",
                        column: x => x.ThanhVienDoiId,
                        principalTable: "ThanhVienDois",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SuKienTrans_ThanhVienDois_ThanhVienId",
                        column: x => x.ThanhVienId,
                        principalTable: "ThanhVienDois",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SuKienTrans_TranDaus_TranDauId",
                        column: x => x.TranDauId,
                        principalTable: "TranDaus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BangDaus_GiaiDauId",
                table: "BangDaus",
                column: "GiaiDauId");

            migrationBuilder.CreateIndex(
                name: "IX_DiemLog_ThoiGian",
                table: "DiemThuongLogs",
                column: "ThoiGian",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_DiemLog_UserId",
                table: "DiemThuongLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DiemThuongLogs_DatSanId",
                table: "DiemThuongLogs",
                column: "DatSanId");

            migrationBuilder.CreateIndex(
                name: "IX_DoiBongs_BangId",
                table: "DoiBongs",
                column: "BangId");

            migrationBuilder.CreateIndex(
                name: "IX_DoiBongs_DoiTruongId",
                table: "DoiBongs",
                column: "DoiTruongId");

            migrationBuilder.CreateIndex(
                name: "IX_DoiBongs_GiaiDauId",
                table: "DoiBongs",
                column: "GiaiDauId");

            migrationBuilder.CreateIndex(
                name: "IX_GiaiDaus_OwnerId",
                table: "GiaiDaus",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_GiaiDaus_SanBongId",
                table: "GiaiDaus",
                column: "SanBongId");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_HetHan",
                table: "OtpCodes",
                column: "NgayHetHan");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_UserId",
                table: "OtpCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SanYeuThich_SanBongId",
                table: "SanYeuThichs",
                column: "SanBongId");

            migrationBuilder.CreateIndex(
                name: "IX_SanYeuThich_UserId",
                table: "SanYeuThichs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_SanYeuThich",
                table: "SanYeuThichs",
                columns: new[] { "UserId", "SanBongId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SuKienTrans_DoiBongId",
                table: "SuKienTrans",
                column: "DoiBongId");

            migrationBuilder.CreateIndex(
                name: "IX_SuKienTrans_DoiId",
                table: "SuKienTrans",
                column: "DoiId");

            migrationBuilder.CreateIndex(
                name: "IX_SuKienTrans_ThanhVienDoiId",
                table: "SuKienTrans",
                column: "ThanhVienDoiId");

            migrationBuilder.CreateIndex(
                name: "IX_SuKienTrans_ThanhVienId",
                table: "SuKienTrans",
                column: "ThanhVienId");

            migrationBuilder.CreateIndex(
                name: "IX_SuKienTrans_TranDauId",
                table: "SuKienTrans",
                column: "TranDauId");

            migrationBuilder.CreateIndex(
                name: "IX_ThanhVienDois_DoiId_SoAo",
                table: "ThanhVienDois",
                columns: new[] { "DoiId", "SoAo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranDaus_BangId",
                table: "TranDaus",
                column: "BangId");

            migrationBuilder.CreateIndex(
                name: "IX_TranDaus_DoiKhachId",
                table: "TranDaus",
                column: "DoiKhachId");

            migrationBuilder.CreateIndex(
                name: "IX_TranDaus_DoiNhaId",
                table: "TranDaus",
                column: "DoiNhaId");

            migrationBuilder.CreateIndex(
                name: "IX_TranDaus_GiaiDauId",
                table: "TranDaus",
                column: "GiaiDauId");

            migrationBuilder.CreateIndex(
                name: "IX_TranDaus_KhungGioId",
                table: "TranDaus",
                column: "KhungGioId");

            migrationBuilder.CreateIndex(
                name: "IX_TranDaus_StaffPhuTrachId",
                table: "TranDaus",
                column: "StaffPhuTrachId");

            migrationBuilder.CreateIndex(
                name: "IX_UserVoucher_HetHan",
                table: "UserVouchers",
                column: "NgayHetHan");

            migrationBuilder.CreateIndex(
                name: "IX_UserVoucher_IsUsed",
                table: "UserVouchers",
                column: "IsUsed");

            migrationBuilder.CreateIndex(
                name: "IX_UserVoucher_UserId",
                table: "UserVouchers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserVouchers_DatSanId",
                table: "UserVouchers",
                column: "DatSanId");

            migrationBuilder.CreateIndex(
                name: "IX_UserVouchers_VoucherId",
                table: "UserVouchers",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "UQ__UserVouc__73EF96E8102BBE1E",
                table: "UserVouchers",
                column: "MaSuDung",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Vouchers__0AAC5B1029A0D8F8",
                table: "Vouchers",
                column: "MaVoucher",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiemThuongLogs");

            migrationBuilder.DropTable(
                name: "OtpCodes");

            migrationBuilder.DropTable(
                name: "SanYeuThichs");

            migrationBuilder.DropTable(
                name: "SuKienTrans");

            migrationBuilder.DropTable(
                name: "UserVouchers");

            migrationBuilder.DropTable(
                name: "ThanhVienDois");

            migrationBuilder.DropTable(
                name: "TranDaus");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "DoiBongs");

            migrationBuilder.DropTable(
                name: "BangDaus");

            migrationBuilder.DropTable(
                name: "GiaiDaus");

            migrationBuilder.DropColumn(
                name: "DaXacThucSdt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DiemHienTai",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NganHang",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SoTaiKhoan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenTaiKhoan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SoSaoCoSoVatChat",
                table: "DanhGias");

            migrationBuilder.DropColumn(
                name: "SoSaoNhanVien",
                table: "DanhGias");
        }
    }
}
