/* =====================================================================
   AddOwnerVoucherAndYeuCauTables.sql
   ---------------------------------------------------------------------
   Tương đương migration: 20260622132514_AddOwnerVoucherAndYeuCauTables
   Mục đích:
     1. Thêm 4 cột mới vào bảng Vouchers (LoaiPhatHanh, OwnerId,
        SanBongId, SoLuotConLai) + index + FK.
     2. Tạo bảng ChuyenNhuongDatSans.
     3. Tạo bảng YeuCauDoiGios.
     4. Tạo bảng YeuCauDoiSans.

   Đặc điểm:
     - Idempotent: chạy lại nhiều lần KHÔNG báo lỗi
       (mọi thao tác đều bọc IF NOT EXISTS / IF EXISTS).
     - Dành cho SQL Server (T-SQL).
     - KHÔNG ghi vào bảng __EFMigrationsHistory => nếu sau này bạn
       quay lại dùng EF Migrations, hãy thêm dòng INSERT ở cuối
       (xem ghi chú cuối file).

   Cách chạy:
     - SSMS / Azure Data Studio: mở file -> Execute (F5) trên DB Web_Stadium.
     - sqlcmd: sqlcmd -S <server> -d <db> -E -i AddOwnerVoucherAndYeuCauTables.sql
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

PRINT N'>>> Bat dau cap nhat schema...';
GO

/* ---------------------------------------------------------------------
   1) BANG Vouchers - them cot moi
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Vouchers')
                 AND name = N'LoaiPhatHanh')
BEGIN
    ALTER TABLE dbo.Vouchers
        ADD LoaiPhatHanh nvarchar(20) NOT NULL
            CONSTRAINT DF_Vouchers_LoaiPhatHanh DEFAULT (N'HeThong');
    PRINT N'  + Vouchers.LoaiPhatHanh added';
END
ELSE
    PRINT N'  = Vouchers.LoaiPhatHanh exists, skip';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Vouchers')
                 AND name = N'OwnerId')
BEGIN
    ALTER TABLE dbo.Vouchers ADD OwnerId int NULL;
    PRINT N'  + Vouchers.OwnerId added';
END
ELSE
    PRINT N'  = Vouchers.OwnerId exists, skip';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Vouchers')
                 AND name = N'SanBongId')
BEGIN
    ALTER TABLE dbo.Vouchers ADD SanBongId int NULL;
    PRINT N'  + Vouchers.SanBongId added';
END
ELSE
    PRINT N'  = Vouchers.SanBongId exists, skip';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Vouchers')
                 AND name = N'SoLuotConLai')
BEGIN
    ALTER TABLE dbo.Vouchers ADD SoLuotConLai int NULL;
    PRINT N'  + Vouchers.SoLuotConLai added';
END
ELSE
    PRINT N'  = Vouchers.SoLuotConLai exists, skip';
GO

/* Index cho Vouchers */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Vouchers_OwnerId'
                 AND object_id = OBJECT_ID(N'dbo.Vouchers'))
BEGIN
    CREATE INDEX IX_Vouchers_OwnerId ON dbo.Vouchers(OwnerId);
    PRINT N'  + IX_Vouchers_OwnerId created';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Vouchers_SanBongId'
                 AND object_id = OBJECT_ID(N'dbo.Vouchers'))
BEGIN
    CREATE INDEX IX_Vouchers_SanBongId ON dbo.Vouchers(SanBongId);
    PRINT N'  + IX_Vouchers_SanBongId created';
END
GO

/* FK cho Vouchers */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Vouchers_Owner')
BEGIN
    ALTER TABLE dbo.Vouchers
        ADD CONSTRAINT FK_Vouchers_Owner
            FOREIGN KEY (OwnerId) REFERENCES dbo.Users(Id);
    PRINT N'  + FK_Vouchers_Owner created';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Vouchers_SanBong')
BEGIN
    ALTER TABLE dbo.Vouchers
        ADD CONSTRAINT FK_Vouchers_SanBong
            FOREIGN KEY (SanBongId) REFERENCES dbo.SanBongs(Id);
    PRINT N'  + FK_Vouchers_SanBong created';
END
GO


/* ---------------------------------------------------------------------
   2) BANG ChuyenNhuongDatSans
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ChuyenNhuongDatSans', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChuyenNhuongDatSans
    (
        Id                  int           IDENTITY(1,1) NOT NULL,
        DatSanId            int           NOT NULL,
        NguoiChuyenId       int           NOT NULL,
        EmailNguoiNhan      nvarchar(150) NULL,
        SdtNguoiNhan        nvarchar(20)  NULL,
        NguoiNhanId         int           NULL,
        LyDo                nvarchar(500) NOT NULL,
        TrangThai           nvarchar(20)  NOT NULL
            CONSTRAINT DF_CNDS_TrangThai DEFAULT (N'ChoPheDuyet'),
        NgayTao             datetime      NOT NULL
            CONSTRAINT DF_CNDS_NgayTao DEFAULT (getdate()),
        NgayXuLy            datetime      NULL,
        NguoiXuLyOwnerId    int           NULL,
        GhiChuXuLy          nvarchar(500) NULL,
        CONSTRAINT PK_ChuyenNhuongDatSans PRIMARY KEY (Id),
        CONSTRAINT FK_CNDS_DatSan
            FOREIGN KEY (DatSanId)         REFERENCES dbo.DatSans(Id),
        CONSTRAINT FK_CNDS_NguoiChuyen
            FOREIGN KEY (NguoiChuyenId)    REFERENCES dbo.Users(Id),
        CONSTRAINT FK_CNDS_NguoiNhan
            FOREIGN KEY (NguoiNhanId)      REFERENCES dbo.Users(Id),
        CONSTRAINT FK_CNDS_OwnerXuLy
            FOREIGN KEY (NguoiXuLyOwnerId) REFERENCES dbo.Users(Id)
    );
    PRINT N'  + Table ChuyenNhuongDatSans created';
END
ELSE
    PRINT N'  = Table ChuyenNhuongDatSans exists, skip';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_CNDS_DatSanId'
                 AND object_id = OBJECT_ID(N'dbo.ChuyenNhuongDatSans'))
    CREATE INDEX IX_CNDS_DatSanId
        ON dbo.ChuyenNhuongDatSans(DatSanId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_CNDS_TrangThai'
                 AND object_id = OBJECT_ID(N'dbo.ChuyenNhuongDatSans'))
    CREATE INDEX IX_CNDS_TrangThai
        ON dbo.ChuyenNhuongDatSans(TrangThai);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_ChuyenNhuongDatSans_NguoiChuyenId'
                 AND object_id = OBJECT_ID(N'dbo.ChuyenNhuongDatSans'))
    CREATE INDEX IX_ChuyenNhuongDatSans_NguoiChuyenId
        ON dbo.ChuyenNhuongDatSans(NguoiChuyenId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_ChuyenNhuongDatSans_NguoiNhanId'
                 AND object_id = OBJECT_ID(N'dbo.ChuyenNhuongDatSans'))
    CREATE INDEX IX_ChuyenNhuongDatSans_NguoiNhanId
        ON dbo.ChuyenNhuongDatSans(NguoiNhanId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_ChuyenNhuongDatSans_NguoiXuLyOwnerId'
                 AND object_id = OBJECT_ID(N'dbo.ChuyenNhuongDatSans'))
    CREATE INDEX IX_ChuyenNhuongDatSans_NguoiXuLyOwnerId
        ON dbo.ChuyenNhuongDatSans(NguoiXuLyOwnerId);
GO


/* ---------------------------------------------------------------------
   3) BANG YeuCauDoiGios
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.YeuCauDoiGios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.YeuCauDoiGios
    (
        Id              int           IDENTITY(1,1) NOT NULL,
        DatSanId        int           NOT NULL,
        KhungGioMoiId   int           NOT NULL,
        NgayThiDauMoi   datetime      NOT NULL,
        LyDo            nvarchar(500) NOT NULL,
        TrangThai       nvarchar(20)  NOT NULL
            CONSTRAINT DF_YCDG_TrangThai DEFAULT (N'ChoPheDuyet'),
        NgayTao         datetime      NOT NULL
            CONSTRAINT DF_YCDG_NgayTao DEFAULT (getdate()),
        NgayXuLy        datetime      NULL,
        NguoiXuLyId     int           NULL,
        GhiChuXuLy      nvarchar(500) NULL,
        CONSTRAINT PK_YeuCauDoiGios PRIMARY KEY (Id),
        CONSTRAINT FK_YCDG_DatSan
            FOREIGN KEY (DatSanId)      REFERENCES dbo.DatSans(Id),
        CONSTRAINT FK_YCDG_KhungGio
            FOREIGN KEY (KhungGioMoiId) REFERENCES dbo.KhungGios(Id),
        CONSTRAINT FK_YCDG_NguoiXuLy
            FOREIGN KEY (NguoiXuLyId)   REFERENCES dbo.Users(Id)
    );
    PRINT N'  + Table YeuCauDoiGios created';
END
ELSE
    PRINT N'  = Table YeuCauDoiGios exists, skip';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_YCDG_DatSanId'
                 AND object_id = OBJECT_ID(N'dbo.YeuCauDoiGios'))
    CREATE INDEX IX_YCDG_DatSanId ON dbo.YeuCauDoiGios(DatSanId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_YCDG_TrangThai'
                 AND object_id = OBJECT_ID(N'dbo.YeuCauDoiGios'))
    CREATE INDEX IX_YCDG_TrangThai ON dbo.YeuCauDoiGios(TrangThai);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_YeuCauDoiGios_KhungGioMoiId'
                 AND object_id = OBJECT_ID(N'dbo.YeuCauDoiGios'))
    CREATE INDEX IX_YeuCauDoiGios_KhungGioMoiId
        ON dbo.YeuCauDoiGios(KhungGioMoiId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_YeuCauDoiGios_NguoiXuLyId'
                 AND object_id = OBJECT_ID(N'dbo.YeuCauDoiGios'))
    CREATE INDEX IX_YeuCauDoiGios_NguoiXuLyId
        ON dbo.YeuCauDoiGios(NguoiXuLyId);
GO


/* ---------------------------------------------------------------------
   4) BANG YeuCauDoiSans
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.YeuCauDoiSans', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.YeuCauDoiSans
    (
        Id              int           IDENTITY(1,1) NOT NULL,
        DatSanId        int           NOT NULL,
        KhungGioMoiId   int           NOT NULL,
        NgayThiDauMoi   datetime      NOT NULL,
        LyDo            nvarchar(500) NOT NULL,
        TrangThai       nvarchar(20)  NOT NULL
            CONSTRAINT DF_YCDS_TrangThai DEFAULT (N'ChoPheDuyet'),
        NgayTao         datetime      NOT NULL
            CONSTRAINT DF_YCDS_NgayTao DEFAULT (getdate()),
        NgayXuLy        datetime      NULL,
        NguoiXuLyId     int           NULL,
        GhiChuXuLy      nvarchar(500) NULL,
        CONSTRAINT PK_YeuCauDoiSans PRIMARY KEY (Id),
        CONSTRAINT FK_YCDS_DatSan
            FOREIGN KEY (DatSanId)      REFERENCES dbo.DatSans(Id),
        CONSTRAINT FK_YCDS_KhungGio
            FOREIGN KEY (KhungGioMoiId) REFERENCES dbo.KhungGios(Id),
        CONSTRAINT FK_YCDS_NguoiXuLy
            FOREIGN KEY (NguoiXuLyId)   REFERENCES dbo.Users(Id)
    );
    PRINT N'  + Table YeuCauDoiSans created';
END
ELSE
    PRINT N'  = Table YeuCauDoiSans exists, skip';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_YCDS_DatSanId'
                 AND object_id = OBJECT_ID(N'dbo.YeuCauDoiSans'))
    CREATE INDEX IX_YCDS_DatSanId ON dbo.YeuCauDoiSans(DatSanId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_YCDS_TrangThai'
                 AND object_id = OBJECT_ID(N'dbo.YeuCauDoiSans'))
    CREATE INDEX IX_YCDS_TrangThai ON dbo.YeuCauDoiSans(TrangThai);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_YeuCauDoiSans_KhungGioMoiId'
                 AND object_id = OBJECT_ID(N'dbo.YeuCauDoiSans'))
    CREATE INDEX IX_YeuCauDoiSans_KhungGioMoiId
        ON dbo.YeuCauDoiSans(KhungGioMoiId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_YeuCauDoiSans_NguoiXuLyId'
                 AND object_id = OBJECT_ID(N'dbo.YeuCauDoiSans'))
    CREATE INDEX IX_YeuCauDoiSans_NguoiXuLyId
        ON dbo.YeuCauDoiSans(NguoiXuLyId);
GO


PRINT N'>>> Hoan tat cap nhat schema.';
GO

/* =====================================================================
   GHI CHU - Neu sau nay ban muon EF Migrations "biet" da chay migration
   nay roi (tranh chay lai), uncomment doan duoi:

   IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
                  WHERE MigrationId = N'20260622132514_AddOwnerVoucherAndYeuCauTables')
   BEGIN
       INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
       VALUES (N'20260622132514_AddOwnerVoucherAndYeuCauTables', N'9.0.0');
   END
   GO
   ===================================================================== */
