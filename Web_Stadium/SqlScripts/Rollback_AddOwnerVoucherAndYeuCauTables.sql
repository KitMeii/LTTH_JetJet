/* =====================================================================
   Rollback_AddOwnerVoucherAndYeuCauTables.sql
   ---------------------------------------------------------------------
   Hoan tac (rollback) cho AddOwnerVoucherAndYeuCauTables.sql
   - Idempotent: chay lai nhieu lan khong loi.
   - CANH BAO: thao tac nay XOA DU LIEU trong 3 bang YeuCau/ChuyenNhuong
     va xoa 4 cot moi cua bang Vouchers. Sao luu truoc khi chay!
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

PRINT N'>>> Bat dau rollback...';
GO

/* 1) Drop FK ben Vouchers truoc */
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Vouchers_Owner')
    ALTER TABLE dbo.Vouchers DROP CONSTRAINT FK_Vouchers_Owner;
GO

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Vouchers_SanBong')
    ALTER TABLE dbo.Vouchers DROP CONSTRAINT FK_Vouchers_SanBong;
GO

/* 2) Drop index ben Vouchers */
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = N'IX_Vouchers_OwnerId'
             AND object_id = OBJECT_ID(N'dbo.Vouchers'))
    DROP INDEX IX_Vouchers_OwnerId ON dbo.Vouchers;
GO

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = N'IX_Vouchers_SanBongId'
             AND object_id = OBJECT_ID(N'dbo.Vouchers'))
    DROP INDEX IX_Vouchers_SanBongId ON dbo.Vouchers;
GO

/* 3) Drop 3 bang moi */
IF OBJECT_ID(N'dbo.ChuyenNhuongDatSans', N'U') IS NOT NULL
    DROP TABLE dbo.ChuyenNhuongDatSans;
GO

IF OBJECT_ID(N'dbo.YeuCauDoiGios', N'U') IS NOT NULL
    DROP TABLE dbo.YeuCauDoiGios;
GO

IF OBJECT_ID(N'dbo.YeuCauDoiSans', N'U') IS NOT NULL
    DROP TABLE dbo.YeuCauDoiSans;
GO

/* 4) Drop 4 cot moi cua Vouchers (kem default constraint neu co) */
IF EXISTS (SELECT 1 FROM sys.default_constraints
           WHERE name = N'DF_Vouchers_LoaiPhatHanh')
    ALTER TABLE dbo.Vouchers DROP CONSTRAINT DF_Vouchers_LoaiPhatHanh;
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.Vouchers') AND name = N'LoaiPhatHanh')
    ALTER TABLE dbo.Vouchers DROP COLUMN LoaiPhatHanh;
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.Vouchers') AND name = N'OwnerId')
    ALTER TABLE dbo.Vouchers DROP COLUMN OwnerId;
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.Vouchers') AND name = N'SanBongId')
    ALTER TABLE dbo.Vouchers DROP COLUMN SanBongId;
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.Vouchers') AND name = N'SoLuotConLai')
    ALTER TABLE dbo.Vouchers DROP COLUMN SoLuotConLai;
GO

/* 5) Xoa khoi bang EFMigrationsHistory (neu da insert) */
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
    DELETE FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = N'20260622132514_AddOwnerVoucherAndYeuCauTables';
GO

PRINT N'>>> Rollback hoan tat.';
GO
