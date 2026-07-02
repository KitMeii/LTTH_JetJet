using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Web_Stadium.EFCore;

public partial class SanBongContext : DbContext
{
    public SanBongContext()
    {
    }

    public SanBongContext(DbContextOptions<SanBongContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<DanhGia> DanhGias { get; set; }

    public virtual DbSet<DanhMucDichVu> DanhMucDichVus { get; set; }

    public virtual DbSet<DanhMucLoaiCo> DanhMucLoaiCos { get; set; }

    public virtual DbSet<DanhMucLoaiSan> DanhMucLoaiSans { get; set; }

    public virtual DbSet<DanhMucQuan> DanhMucQuans { get; set; }

    public virtual DbSet<DatSan> DatSans { get; set; }

    public virtual DbSet<DatSanDichVu> DatSanDichVus { get; set; }

    public virtual DbSet<DichVu> DichVus { get; set; }

    public virtual DbSet<KhieuNai> KhieuNais { get; set; }

    public virtual DbSet<KhungGio> KhungGios { get; set; }

    public virtual DbSet<Matchmaking> Matchmakings { get; set; }

    public virtual DbSet<SanBong> SanBongs { get; set; }

    public virtual DbSet<StaffSanPhanCong> StaffSanPhanCongs { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<VungKhuVuc> VungKhuVucs { get; set; }

    public virtual DbSet<AnhSanBong> AnhSanBongs { get; set; }

    // ── v5: Bảng mới ──────────────────────────────────────────
    public virtual DbSet<DiemThuongLog> DiemThuongLogs { get; set; }
    public virtual DbSet<OtpCode> OtpCodes { get; set; }
    public virtual DbSet<SanYeuThich> SanYeuThichs { get; set; }
    public virtual DbSet<UserVoucher> UserVouchers { get; set; }
    public virtual DbSet<Voucher> Vouchers { get; set; }
    public virtual DbSet<GiaoDichHoanCoc> GiaoDichHoanCocs { get; set; }

    // - V6: 6 bang moi
    public virtual DbSet<BangDau> BangDaus { get; set; }
    public virtual DbSet<DoiBong> DoiBongs { get; set; }
    public virtual DbSet<GiaiDau> GiaiDaus { get; set; }
    public virtual DbSet<SuKienTran> SuKienTrans { get; set; }
    public virtual DbSet<ThanhVienDoi> ThanhVienDois { get; set; }
    public virtual DbSet<TranDau> TranDaus { get; set; }

    // ── UC068-UC071: Owner ops ─────────────────────────────────
    public virtual DbSet<YeuCauDoiGio> YeuCauDoiGios { get; set; }
    public virtual DbSet<YeuCauDoiSan> YeuCauDoiSans { get; set; }
    public virtual DbSet<ChuyenNhuongDatSan> ChuyenNhuongDatSans { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlServer("Data Source=NEMMM\\CNAMM;Initial Catalog=SanBongBTL;Integrated Security=True;Encrypt=True;Trust Server Certificate=True;MultipleActiveResultSets=True");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AuditLog__3214EC07B98758BC");

            entity.HasIndex(e => e.HanhDong, "IX_AuditLogs_HanhDong");

            entity.HasIndex(e => e.ThoiGian, "IX_AuditLogs_ThoiGian");

            entity.HasIndex(e => e.UserId, "IX_AuditLogs_UserId");

            entity.Property(e => e.DoiTuong).HasMaxLength(50);
            entity.Property(e => e.HanhDong).HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.MoTa).HasMaxLength(500);
            entity.Property(e => e.ThoiGian)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.VaiTro).HasMaxLength(20);

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AuditLogs_User");
        });

        modelBuilder.Entity<DanhGia>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DanhGias__3214EC07D929FE0E");

            entity.HasIndex(e => new { e.UserId, e.DatSanId }, "UQ_DanhGia_User_DatSan").IsUnique();

            entity.Property(e => e.NgayDanhGia)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NhanXet).HasMaxLength(1000);
            entity.Property(e => e.SoSao).HasDefaultValue(5);

            entity.HasOne(d => d.DatSan).WithMany(p => p.DanhGia)
                .HasForeignKey(d => d.DatSanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DanhGias_DatSan");

            entity.HasOne(d => d.SanBong).WithMany(p => p.DanhGia)
                .HasForeignKey(d => d.SanBongId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DanhGias_SanBong");

            entity.HasOne(d => d.User).WithMany(p => p.DanhGia)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DanhGias_User");
        });

        modelBuilder.Entity<DanhMucDichVu>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DanhMucD__3214EC074DA53941");

            entity.ToTable("DanhMucDichVu");

            entity.Property(e => e.Icon).HasMaxLength(10);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MoTa).HasMaxLength(500);
            entity.Property(e => e.TenDichVu).HasMaxLength(100);
        });

        modelBuilder.Entity<DanhMucLoaiCo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DanhMucL__3214EC078B9DADB2");

            entity.ToTable("DanhMucLoaiCo");

            entity.HasIndex(e => e.MaLoai, "UQ__DanhMucL__730A5758B292C788").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MaLoai).HasMaxLength(20);
            entity.Property(e => e.TenLoai).HasMaxLength(100);
        });

        modelBuilder.Entity<DanhMucLoaiSan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DanhMucL__3214EC0729420409");

            entity.ToTable("DanhMucLoaiSan");

            entity.HasIndex(e => e.MaLoai, "UQ__DanhMucL__730A5758D447AFF2").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MaLoai).HasMaxLength(5);
            entity.Property(e => e.TenLoai).HasMaxLength(50);
        });

        modelBuilder.Entity<DanhMucQuan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DanhMucQ__3214EC0729878EAD");

            entity.ToTable("DanhMucQuan");

            entity.HasIndex(e => e.TenQuan, "UQ__DanhMucQ__73528DBBAC1BD689").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TenQuan).HasMaxLength(100);
            entity.Property(e => e.ThanhPho)
                .HasMaxLength(100)
                .HasDefaultValue("Hà Nội");

            entity.HasOne(d => d.VungKhuVuc).WithMany(p => p.DanhMucQuans)
                .HasForeignKey(d => d.VungKhuVucId)
                .HasConstraintName("FK_DanhMucQuan_Vung");
        });

        modelBuilder.Entity<DatSan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DatSans__3214EC07965DF6D4");

            entity.HasIndex(e => e.NgayThiDau, "IX_DatSans_NgayThiDau");

            entity.HasIndex(e => e.StaffCheckInId, "IX_DatSans_StaffCheckIn");

            entity.HasIndex(e => e.TrangThai, "IX_DatSans_TrangThai");

            entity.HasIndex(e => e.UserId, "IX_DatSans_UserId");

            entity.HasIndex(e => e.MaXacNhan, "UQ__DatSans__02DF438457E964F2").IsUnique();

            entity.Property(e => e.GhiChuSuCo).HasMaxLength(500);
            entity.Property(e => e.LoaiSuCo).HasMaxLength(20);
            entity.Property(e => e.MaXacNhan).HasMaxLength(50);
            entity.Property(e => e.NgayThiDau).HasColumnType("datetime");
            entity.Property(e => e.ThoiGianTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TienCoc).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TongTien).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .HasDefaultValue("ChoDuyet");

            entity.HasOne(d => d.KhungGio).WithMany(p => p.DatSans)
                .HasForeignKey(d => d.KhungGioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DatSans_KhungGio");

            entity.HasOne(d => d.StaffCheckIn).WithMany(p => p.DatSanStaffCheckIns)
                .HasForeignKey(d => d.StaffCheckInId)
                .HasConstraintName("FK_DatSans_StaffCheckIn");

            entity.HasOne(d => d.StaffCheckOut).WithMany(p => p.DatSanStaffCheckOuts)
                .HasForeignKey(d => d.StaffCheckOutId)
                .HasConstraintName("FK_DatSans_StaffCheckOut");

            entity.HasOne(d => d.User).WithMany(p => p.DatSanUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DatSans_User");
        });

        modelBuilder.Entity<DatSanDichVu>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DatSan_D__3214EC07CF8773E8");

            entity.ToTable("DatSan_DichVus");

            entity.Property(e => e.SoLuong).HasDefaultValue(1);

            entity.HasOne(d => d.DatSan).WithMany(p => p.DatSanDichVus)
                .HasForeignKey(d => d.DatSanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DatSanDichVu_DatSan");

            entity.HasOne(d => d.DichVu).WithMany(p => p.DatSanDichVus)
                .HasForeignKey(d => d.DichVuId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DatSanDichVu_DichVu");
        });

        modelBuilder.Entity<DichVu>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DichVus__3214EC0794085133");

            entity.HasIndex(e => e.SanBongId, "IX_DichVus_SanBongId");

            entity.Property(e => e.Gia).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MoTa).HasMaxLength(500);
            entity.Property(e => e.TenDichVu).HasMaxLength(100);

            entity.HasOne(d => d.DanhMucDichVu).WithMany(p => p.DichVus)
                .HasForeignKey(d => d.DanhMucDichVuId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DichVus_DanhMuc");

            entity.HasOne(d => d.SanBong).WithMany(p => p.DichVus)
                .HasForeignKey(d => d.SanBongId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DichVus_SanBong");
        });

        modelBuilder.Entity<KhieuNai>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__KhieuNai__3214EC07331207BC");

            entity.HasIndex(e => e.TrangThai, "IX_KhieuNais_TrangThai");

            entity.Property(e => e.GhiChuAdmin).HasMaxLength(500);
            entity.Property(e => e.LyDo).HasMaxLength(1000);
            entity.Property(e => e.NgayGui)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NgayXuLy).HasColumnType("datetime");
            entity.Property(e => e.SoTienHoan).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .HasDefaultValue("ChoXuLy");

            entity.HasOne(d => d.AdminXuLy).WithMany(p => p.KhieuNaiAdminXuLies)
                .HasForeignKey(d => d.AdminXuLyId)
                .HasConstraintName("FK_KhieuNais_Admin");

            entity.HasOne(d => d.DatSan).WithMany(p => p.KhieuNais)
                .HasForeignKey(d => d.DatSanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KhieuNais_DatSan");

            entity.HasOne(d => d.User).WithMany(p => p.KhieuNaiUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KhieuNais_User");
        });

        modelBuilder.Entity<KhungGio>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__KhungGio__3214EC07BBE9DFC3");

            entity.HasIndex(e => e.LoaiNgay, "IX_KhungGios_LoaiNgay");

            entity.HasIndex(e => e.SanBongId, "IX_KhungGios_SanBongId");

            entity.HasIndex(e => e.TrangThai, "IX_KhungGios_TrangThai");

            entity.Property(e => e.Gia).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GiaCuoiTuan).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GiaGioVang).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.LoaiNgay)
                .HasMaxLength(20)
                .HasDefaultValue("TatCa");
            entity.Property(e => e.ThoiGianHetGiuCho).HasColumnType("datetime");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .HasDefaultValue("Trong");

            entity.HasOne(d => d.SanBong).WithMany(p => p.KhungGios)
                .HasForeignKey(d => d.SanBongId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KhungGios_SanBong");
        });

        modelBuilder.Entity<Matchmaking>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Matchmak__3214EC07934708EB");

            entity.HasIndex(e => e.TrangThai, "IX_Matchmakings_TrangThai");

            entity.HasIndex(e => e.DatSanId, "UQ__Matchmak__AE3C65EBA19D6212").IsUnique();

            entity.Property(e => e.MoTa).HasMaxLength(1000);
            entity.Property(e => e.NgayDang)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoNguoiCanThem).HasDefaultValue(1);
            entity.Property(e => e.TieuDe).HasMaxLength(200);
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .HasDefaultValue("DangTim");

            entity.HasOne(d => d.DatSan).WithOne(p => p.Matchmaking)
                .HasForeignKey<Matchmaking>(d => d.DatSanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Matchmakings_DatSan");

            entity.HasOne(d => d.User).WithMany(p => p.Matchmakings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Matchmakings_User");
        });

        modelBuilder.Entity<SanBong>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SanBongs__3214EC07B553D465");

            entity.HasIndex(e => e.IsHidden, "IX_SanBongs_IsHidden");

            entity.HasIndex(e => e.OwnerId, "IX_SanBongs_OwnerId");

            entity.HasIndex(e => e.Quan, "IX_SanBongs_Quan");

            entity.HasIndex(e => e.TrangThaiDuyet, "IX_SanBongs_TrangThai");

            entity.Property(e => e.DiaChi).HasMaxLength(300);
            entity.Property(e => e.HinhAnh).HasMaxLength(500);
            entity.Property(e => e.LoaiCo).HasMaxLength(50);
            entity.Property(e => e.LoaiSan).HasMaxLength(5);
            entity.Property(e => e.MoTa).HasDefaultValue("");
            entity.Property(e => e.NgayKyHopDong).HasColumnType("datetime");
            entity.Property(e => e.Quan).HasMaxLength(100);
            entity.Property(e => e.TenSan).HasMaxLength(200);
            entity.Property(e => e.ThanhPho).HasMaxLength(100);
            entity.Property(e => e.TrangThaiDuyet)
                .HasMaxLength(20)
                .HasDefaultValue("ChoDuyet");
            entity.Property(e => e.TyLeCoc)
                .HasDefaultValue(0.30m)
                .HasColumnType("decimal(3, 2)");

            entity.HasOne(d => d.Owner).WithMany(p => p.SanBongs)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SanBongs_Owner");
        });


        modelBuilder.Entity<AnhSanBong>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("AnhSanBongs");

            entity.HasIndex(e => new { e.SanBongId, e.ThuTu }, "IX_AnhSanBongs_SanBong");

            entity.Property(e => e.DuongDan).HasMaxLength(1000);
            entity.Property(e => e.LoaiAnh).HasMaxLength(20).HasDefaultValue("Upload");
            entity.Property(e => e.MoTa).HasMaxLength(200);
            entity.Property(e => e.NgayThem)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.SanBong)
                .WithMany(p => p.AnhSanBongs)
                .HasForeignKey(d => d.SanBongId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AnhSanBongs_SanBong");
        });

        modelBuilder.Entity<StaffSanPhanCong>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StaffSan__3214EC07A46AE590");

            entity.ToTable("StaffSanPhanCong");

            entity.HasIndex(e => e.SanBongId, "IX_StaffSan_SanBongId");

            entity.HasIndex(e => e.StaffId, "IX_StaffSan_StaffId");

            entity.HasIndex(e => new { e.StaffId, e.SanBongId }, "UQ_StaffSan").IsUnique();

            entity.Property(e => e.NgayGan)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.SanBong).WithMany(p => p.StaffSanPhanCongs)
                .HasForeignKey(d => d.SanBongId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffSan_SanBong");

            entity.HasOne(d => d.Staff).WithMany(p => p.StaffSanPhanCongs)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffSan_Staff");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC0731B6ED02");

            entity.HasIndex(e => e.OwnerIdCuaStaff, "IX_Users_OwnerIdCuaStaff");

            entity.HasIndex(e => e.VaiTro, "IX_Users_VaiTro");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D10534A9983397").IsUnique();

            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.HoTen).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MatKhau).HasMaxLength(255);
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoDienThoai).HasMaxLength(20);
            entity.Property(e => e.VaiTro)
                .HasMaxLength(20)
                .HasDefaultValue("User");

            entity.HasOne(d => d.OwnerIdCuaStaffNavigation).WithMany(p => p.InverseOwnerIdCuaStaffNavigation)
                .HasForeignKey(d => d.OwnerIdCuaStaff)
                .HasConstraintName("FK_Users_OwnerCuaStaff");
        });

        modelBuilder.Entity<VungKhuVuc>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__VungKhuV__3214EC07658D9681");

            entity.HasIndex(e => e.TenVung, "UQ__VungKhuV__D64F707F63384C9C").IsUnique();

            entity.Property(e => e.DefaultZoom).HasDefaultValue(12);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Lat).HasDefaultValue(21.028500000000001);
            entity.Property(e => e.Lng).HasDefaultValue(105.85420000000001);
            entity.Property(e => e.MauSac)
                .HasMaxLength(10)
                .HasDefaultValue("#1ed760");
            entity.Property(e => e.MoTa).HasMaxLength(300);
            entity.Property(e => e.TenVung).HasMaxLength(100);
            entity.Property(e => e.TyLeHoaHong)
                .HasDefaultValue(0.10m)
                .HasColumnType("decimal(3, 2)");
        });

        // ── v5: Config bảng mới ───────────────────────────────
        modelBuilder.Entity<DiemThuongLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DiemThuo__3214EC07082AC8BB");
            entity.HasIndex(e => e.ThoiGian, "IX_DiemLog_ThoiGian").IsDescending();
            entity.HasIndex(e => e.UserId, "IX_DiemLog_UserId");
            entity.Property(e => e.GhiChu).HasMaxLength(200);
            entity.Property(e => e.LoaiSuKien).HasMaxLength(50);
            entity.Property(e => e.SoDuSauGd).HasColumnName("SoDuSauGD");
            entity.Property(e => e.ThoiGian)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.HasOne(d => d.DatSan).WithMany(p => p.DiemThuongLogs)
                .HasForeignKey(d => d.DatSanId)
                .HasConstraintName("FK_DiemLog_DatSan");
            entity.HasOne(d => d.User).WithMany(p => p.DiemThuongLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DiemLog_User");
        });

        modelBuilder.Entity<OtpCode>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__OtpCodes__3214EC072CAD03AF");
            entity.HasIndex(e => e.NgayHetHan, "IX_OtpCodes_HetHan");
            entity.HasIndex(e => e.UserId, "IX_OtpCodes_UserId");
            entity.Property(e => e.MaOtp).HasMaxLength(10);
            entity.Property(e => e.NgayHetHan)
                .HasDefaultValueSql("(dateadd(minute,(5),getdate()))")
                .HasColumnType("datetime");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoDienThoai).HasMaxLength(20);
            entity.HasOne(d => d.User).WithMany(p => p.OtpCodes)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OtpCodes_User");
        });

        modelBuilder.Entity<SanYeuThich>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SanYeuTh__3214EC07643721CD");
            entity.HasIndex(e => e.SanBongId, "IX_SanYeuThich_SanBongId");
            entity.HasIndex(e => e.UserId, "IX_SanYeuThich_UserId");
            entity.HasIndex(e => new { e.UserId, e.SanBongId }, "UQ_SanYeuThich").IsUnique();
            entity.Property(e => e.NgayThem)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.HasOne(d => d.SanBong).WithMany(p => p.SanYeuThiches)
                .HasForeignKey(d => d.SanBongId)
                .HasConstraintName("FK_SanYeuThich_SanBong");
            entity.HasOne(d => d.User).WithMany(p => p.SanYeuThiches)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_SanYeuThich_User");
        });

        modelBuilder.Entity<UserVoucher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserVouc__3214EC0796E42189");
            entity.HasIndex(e => e.NgayHetHan, "IX_UserVoucher_HetHan");
            entity.HasIndex(e => e.IsUsed, "IX_UserVoucher_IsUsed");
            entity.HasIndex(e => e.UserId, "IX_UserVoucher_UserId");
            entity.HasIndex(e => e.MaSuDung, "UQ__UserVouc__73EF96E8102BBE1E").IsUnique();
            entity.Property(e => e.MaSuDung).HasMaxLength(50);
            entity.Property(e => e.NgayDoi)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NgayHetHan).HasColumnType("datetime");
            entity.Property(e => e.NgaySuDung).HasColumnType("datetime");
            entity.HasOne(d => d.DatSan).WithMany(p => p.UserVouchers)
                .HasForeignKey(d => d.DatSanId)
                .HasConstraintName("FK_UserVoucher_DatSan");
            entity.HasOne(d => d.User).WithMany(p => p.UserVouchers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserVoucher_User");
            entity.HasOne(d => d.Voucher).WithMany(p => p.UserVouchers)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserVoucher_Voucher");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Vouchers__3214EC07092807B4");
            entity.HasIndex(e => e.MaVoucher, "UQ__Vouchers__0AAC5B1029A0D8F8").IsUnique();
            entity.Property(e => e.GiaTriGiam).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GiamToiDa).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LoaiGiam)
                .HasMaxLength(20)
                .HasDefaultValue("PhanTram");
            entity.Property(e => e.MaVoucher).HasMaxLength(50);
            entity.Property(e => e.MoTa).HasMaxLength(500);
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoNgayHieuLuc).HasDefaultValue(30);
            entity.Property(e => e.TenVoucher).HasMaxLength(200);
            entity.Property(e => e.LoaiPhatHanh)
                .HasMaxLength(20)
                .HasDefaultValue("HeThong");

            entity.HasIndex(e => e.OwnerId, "IX_Vouchers_OwnerId");
            entity.HasIndex(e => e.SanBongId, "IX_Vouchers_SanBongId");

            entity.HasOne(d => d.Owner).WithMany()
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Vouchers_Owner");

            entity.HasOne(d => d.SanBong).WithMany()
                .HasForeignKey(d => d.SanBongId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Vouchers_SanBong");
        });

        // ── UC069: YeuCauDoiGio ───────────────────────────────
        modelBuilder.Entity<YeuCauDoiGio>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("YeuCauDoiGios");

            entity.HasIndex(e => e.DatSanId, "IX_YCDG_DatSanId");
            entity.HasIndex(e => e.TrangThai, "IX_YCDG_TrangThai");

            entity.Property(e => e.LyDo).HasMaxLength(500);
            entity.Property(e => e.GhiChuXuLy).HasMaxLength(500);
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .HasDefaultValue("ChoPheDuyet");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NgayThiDauMoi).HasColumnType("datetime");
            entity.Property(e => e.NgayXuLy).HasColumnType("datetime");

            entity.HasOne(d => d.DatSan).WithMany()
                .HasForeignKey(d => d.DatSanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_YCDG_DatSan");

            entity.HasOne(d => d.KhungGioMoi).WithMany()
                .HasForeignKey(d => d.KhungGioMoiId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_YCDG_KhungGio");

            entity.HasOne(d => d.NguoiXuLy).WithMany()
                .HasForeignKey(d => d.NguoiXuLyId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_YCDG_NguoiXuLy");
        });

        // ── UC070: YeuCauDoiSan ───────────────────────────────
        modelBuilder.Entity<YeuCauDoiSan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("YeuCauDoiSans");

            entity.HasIndex(e => e.DatSanId, "IX_YCDS_DatSanId");
            entity.HasIndex(e => e.TrangThai, "IX_YCDS_TrangThai");

            entity.Property(e => e.LyDo).HasMaxLength(500);
            entity.Property(e => e.GhiChuXuLy).HasMaxLength(500);
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .HasDefaultValue("ChoPheDuyet");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NgayThiDauMoi).HasColumnType("datetime");
            entity.Property(e => e.NgayXuLy).HasColumnType("datetime");

            entity.HasOne(d => d.DatSan).WithMany()
                .HasForeignKey(d => d.DatSanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_YCDS_DatSan");

            entity.HasOne(d => d.KhungGioMoi).WithMany()
                .HasForeignKey(d => d.KhungGioMoiId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_YCDS_KhungGio");

            entity.HasOne(d => d.NguoiXuLy).WithMany()
                .HasForeignKey(d => d.NguoiXuLyId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_YCDS_NguoiXuLy");
        });

        // ── UC071: ChuyenNhuongDatSan ─────────────────────────
        modelBuilder.Entity<ChuyenNhuongDatSan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("ChuyenNhuongDatSans");

            entity.HasIndex(e => e.DatSanId, "IX_CNDS_DatSanId");
            entity.HasIndex(e => e.TrangThai, "IX_CNDS_TrangThai");

            entity.Property(e => e.EmailNguoiNhan).HasMaxLength(150);
            entity.Property(e => e.SdtNguoiNhan).HasMaxLength(20);
            entity.Property(e => e.LyDo).HasMaxLength(500);
            entity.Property(e => e.GhiChuXuLy).HasMaxLength(500);
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .HasDefaultValue("ChoPheDuyet");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NgayXuLy).HasColumnType("datetime");

            entity.HasOne(d => d.DatSan).WithMany()
                .HasForeignKey(d => d.DatSanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CNDS_DatSan");

            entity.HasOne(d => d.NguoiChuyen).WithMany()
                .HasForeignKey(d => d.NguoiChuyenId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CNDS_NguoiChuyen");

            entity.HasOne(d => d.NguoiNhan).WithMany()
                .HasForeignKey(d => d.NguoiNhanId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CNDS_NguoiNhan");

            entity.HasOne(d => d.NguoiXuLyOwner).WithMany()
                .HasForeignKey(d => d.NguoiXuLyOwnerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CNDS_OwnerXuLy");
        });

        modelBuilder.Entity<GiaoDichHoanCoc>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__GiaoDich__3214EC07A1B2C3D4");

            entity.HasIndex(e => e.DatSanId, "IX_GiaoDichHoanCoc_DatSanId");
            entity.HasIndex(e => e.ThoiGianGiaoDich, "IX_GiaoDichHoanCoc_ThoiGian");
            entity.HasIndex(e => e.VaiTroNguoiKhoiTao, "IX_GiaoDichHoanCoc_VaiTro");

            entity.Property(e => e.ThoiGianGiaoDich)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoTien).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VaiTroNguoiKhoiTao).HasMaxLength(20);
            entity.Property(e => e.TrangThaiHoan).HasMaxLength(20);
            entity.Property(e => e.GhiChu).HasMaxLength(500);

            entity.HasOne(d => d.DatSan).WithMany(p => p.GiaoDichHoanCocs)
                .HasForeignKey(d => d.DatSanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GiaoDichHoanCoc_DatSan");

            entity.HasOne(d => d.NguoiKhoiTao).WithMany()
                .HasForeignKey(d => d.NguoiKhoiTaoId)
                .HasConstraintName("FK_GiaoDichHoanCoc_User");
        });

        // ngày 22/5/2026   : Config 6 bảng mới của V6
        modelBuilder.Entity<GiaiDau>(entity => {
            entity.HasOne(d => d.SanBong).WithMany()
                .HasForeignKey(d => d.SanBongId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.Owner).WithMany()
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.NoAction);
            // Staff phụ trách toàn giải (Owner gán)
            entity.HasOne(d => d.StaffPhuTrach).WithMany()
                .HasForeignKey(d => d.StaffPhuTrachId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Dummy Booking khóa slot sân cho giải đấu (giai đoạn 1 blueprint)
        modelBuilder.Entity<DatSan>(entity => {
            entity.HasOne(d => d.GiaiDau)
                .WithMany(g => g.DatSans)
                .HasForeignKey(d => d.GiaiDauId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<BangDau>(entity => {
            entity.HasOne(d => d.GiaiDau)
                .WithMany(g => g.BangDaus)
                .HasForeignKey(d => d.GiaiDauId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DoiBong>(entity => {
            entity.HasOne(d => d.GiaiDau)
                .WithMany(g => g.DoiBongs)
                .HasForeignKey(d => d.GiaiDauId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.Bang).WithMany(b => b.DoiBongs)
                .HasForeignKey(d => d.BangId);
            entity.HasOne(d => d.DoiTruong).WithMany()
                .HasForeignKey(d => d.DoiTruongId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ThanhVienDoi>(entity => {
            entity.HasOne(d => d.Doi)
                .WithMany(d => d.ThanhViens)
                .HasForeignKey(d => d.DoiId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(d => new { d.DoiId, d.SoAo }).IsUnique();
        });

        modelBuilder.Entity<TranDau>(entity => {
            entity.HasOne(d => d.GiaiDau)
                .WithMany(g => g.TranDaus)
                .HasForeignKey(d => d.GiaiDauId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.BangDau)
                .WithMany(b => b.TranDaus)
                .HasForeignKey(d => d.BangId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.DoiNha)
                .WithMany(d => d.TranDauDoiNhas)
                .HasForeignKey(d => d.DoiNhaId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.DoiKhach)
                .WithMany(d => d.TranDauDoiKhachs)
                .HasForeignKey(d => d.DoiKhachId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.KhungGio).WithMany()
                .HasForeignKey(d => d.KhungGioId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.StaffPhuTrach).WithMany()
                .HasForeignKey(d => d.StaffPhuTrachId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SuKienTran>(entity => {
            entity.HasOne(d => d.TranDau)
                .WithMany(t => t.SuKiens)
                .HasForeignKey(d => d.TranDauId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.ThanhVien).WithMany()
                .HasForeignKey(d => d.ThanhVienId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.Doi).WithMany()
                .HasForeignKey(d => d.DoiId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}