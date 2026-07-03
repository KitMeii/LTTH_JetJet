-- ═══════════════════════════════════════════════════════════════════
-- Migration: AddAutoModeGiaiDau
-- Ngày: 2026-07-03
-- Mô tả: Thêm cột AutoMode (bit) vào bảng GiaiDaus để bật chế độ
--        tự động chia bảng + xếp lịch khi Owner bấm Đóng đăng ký.
--
-- Cách chạy:
--   sqlcmd -S localhost -d SanBongBTL -i AddAutoModeGiaiDau.sql
--   (hoặc paste vào SSMS / Azure Data Studio → chọn DB → F5)
-- ═══════════════════════════════════════════════════════════════════

BEGIN TRANSACTION;

-- Chống chạy lại: chỉ thêm nếu cột chưa tồn tại
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[GiaiDaus]')
      AND name = N'AutoMode'
)
BEGIN
    ALTER TABLE [dbo].[GiaiDaus]
    ADD [AutoMode] BIT NOT NULL CONSTRAINT [DF_GiaiDaus_AutoMode] DEFAULT (0);

    PRINT N'✅ Đã thêm cột AutoMode vào GiaiDaus (default 0).';
END
ELSE
BEGIN
    PRINT N'ℹ️  Cột AutoMode đã tồn tại — bỏ qua.';
END

-- Ghi vào __EFMigrationsHistory để EF biết migration này đã áp dụng.
-- Tránh việc `dotnet ef database update` sau này tưởng chưa chạy và
-- áp dụng lại rồi lỗi "column already exists".
IF NOT EXISTS (
    SELECT 1 FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260703103000_AddAutoModeGiaiDau'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260703103000_AddAutoModeGiaiDau', N'8.0.25');

    PRINT N'✅ Đã ghi __EFMigrationsHistory.';
END

COMMIT TRANSACTION;
GO

-- ── Rollback (dán riêng khi cần gỡ) ────────────────────────────────
-- ALTER TABLE [dbo].[GiaiDaus] DROP CONSTRAINT [DF_GiaiDaus_AutoMode];
-- ALTER TABLE [dbo].[GiaiDaus] DROP COLUMN [AutoMode];
-- DELETE FROM [dbo].[__EFMigrationsHistory]
--   WHERE [MigrationId] = N'20260703103000_AddAutoModeGiaiDau';
