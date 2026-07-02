-- Patch: bổ sung các cột legacy voucher/discount còn thiếu do migration
-- 20260617030021_AddVoucherSystem chưa được apply lên DB SanBongBTL.
-- Idempotent: dùng IF NOT EXISTS trên sys.columns / sys.indexes.
SET NOCOUNT ON;

-- ============ DatSans: 5 cột legacy ============
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DatSans') AND name = 'TienGoc')
    ALTER TABLE dbo.DatSans ADD TienGoc decimal(18,2) NOT NULL CONSTRAINT DF_DatSans_TienGoc DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DatSans') AND name = 'TienGiamSan')
    ALTER TABLE dbo.DatSans ADD TienGiamSan decimal(18,2) NOT NULL CONSTRAINT DF_DatSans_TienGiamSan DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DatSans') AND name = 'TienGiamHeThong')
    ALTER TABLE dbo.DatSans ADD TienGiamHeThong decimal(18,2) NOT NULL CONSTRAINT DF_DatSans_TienGiamHeThong DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DatSans') AND name = 'VoucherSanId')
    ALTER TABLE dbo.DatSans ADD VoucherSanId int NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DatSans') AND name = 'VoucherHeThongId')
    ALTER TABLE dbo.DatSans ADD VoucherHeThongId int NULL;

-- Index cho VoucherSanId / VoucherHeThongId (match Fluent API HasIndex)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.DatSans') AND name = 'IX_DatSans_VoucherSanId')
    CREATE INDEX IX_DatSans_VoucherSanId ON dbo.DatSans(VoucherSanId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.DatSans') AND name = 'IX_DatSans_VoucherHeThongId')
    CREATE INDEX IX_DatSans_VoucherHeThongId ON dbo.DatSans(VoucherHeThongId);

-- ============ Vouchers: 6 cột legacy ============
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Vouchers') AND name = 'LoaiVoucher')
    ALTER TABLE dbo.Vouchers ADD LoaiVoucher nvarchar(20) NOT NULL CONSTRAINT DF_Vouchers_LoaiVoucher DEFAULT 'HeThong';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Vouchers') AND name = 'SoLuong')
    ALTER TABLE dbo.Vouchers ADD SoLuong int NOT NULL CONSTRAINT DF_Vouchers_SoLuong DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Vouchers') AND name = 'DaDung')
    ALTER TABLE dbo.Vouchers ADD DaDung int NOT NULL CONSTRAINT DF_Vouchers_DaDung DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Vouchers') AND name = 'NgayBatDau')
    ALTER TABLE dbo.Vouchers ADD NgayBatDau datetime NOT NULL CONSTRAINT DF_Vouchers_NgayBatDau DEFAULT '1900-01-01';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Vouchers') AND name = 'NgayHetHan')
    ALTER TABLE dbo.Vouchers ADD NgayHetHan datetime NOT NULL CONSTRAINT DF_Vouchers_NgayHetHan DEFAULT '2099-12-31';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Vouchers') AND name = 'DieuKienToiThieu')
    ALTER TABLE dbo.Vouchers ADD DieuKienToiThieu decimal(18,2) NOT NULL CONSTRAINT DF_Vouchers_DieuKienToiThieu DEFAULT 0;

-- Index legacy (match Fluent API HasIndex)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Vouchers') AND name = 'IX_Vouchers_LoaiVoucher')
    CREATE INDEX IX_Vouchers_LoaiVoucher ON dbo.Vouchers(LoaiVoucher);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Vouchers') AND name = 'IX_Vouchers_NgayHetHan')
    CREATE INDEX IX_Vouchers_NgayHetHan ON dbo.Vouchers(NgayHetHan);

PRINT 'Patch legacy voucher fields applied.';
