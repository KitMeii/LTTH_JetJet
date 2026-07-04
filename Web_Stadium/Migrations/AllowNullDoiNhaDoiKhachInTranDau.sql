-- ═══════════════════════════════════════════════════════════════════
-- Migration: AllowNullDoiNhaDoiKhachInTranDau
-- Ngày: 2026-07-03
-- Mô tả: Cho phép TranDaus.DoiNhaId và DoiKhachId nhận NULL để làm
--        placeholder cho Bán kết / Chung kết ở giai đoạn "Pending"
--        (chờ kết quả vòng trước).
--
--        Trước đây KnockOutService dùng DoiNhaId = 0 làm placeholder
--        nhưng vi phạm FK (không có DoiBong với Id = 0) → SaveChanges
--        báo lỗi. Fix bằng cách:
--          1) Drop FK cứng.
--          2) ALTER COLUMN … NULL.
--          3) Recreate FK (vẫn NoAction để không cascade delete đội).
--
-- Cách chạy:
--   sqlcmd -S localhost -d SanBongBTL -i AllowNullDoiNhaDoiKhachInTranDau.sql
--   (hoặc paste vào SSMS / Azure Data Studio → chọn DB → F5)
-- ═══════════════════════════════════════════════════════════════════

BEGIN TRANSACTION;

-- ── Chống chạy lại: chỉ làm nếu 1 trong 2 cột đang NOT NULL ────────
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[TranDaus]')
      AND name IN (N'DoiNhaId', N'DoiKhachId')
      AND is_nullable = 0
)
BEGIN
    -- 1) Drop 2 FK cứng
    IF EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name = N'FK_TranDaus_DoiBongs_DoiNhaId')
    BEGIN
        ALTER TABLE [dbo].[TranDaus] DROP CONSTRAINT [FK_TranDaus_DoiBongs_DoiNhaId];
        PRINT N'✓ Đã drop FK_TranDaus_DoiBongs_DoiNhaId.';
    END

    IF EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name = N'FK_TranDaus_DoiBongs_DoiKhachId')
    BEGIN
        ALTER TABLE [dbo].[TranDaus] DROP CONSTRAINT [FK_TranDaus_DoiBongs_DoiKhachId];
        PRINT N'✓ Đã drop FK_TranDaus_DoiBongs_DoiKhachId.';
    END

    -- 2) Alter cột về NULL
    ALTER TABLE [dbo].[TranDaus] ALTER COLUMN [DoiNhaId]   INT NULL;
    ALTER TABLE [dbo].[TranDaus] ALTER COLUMN [DoiKhachId] INT NULL;
    PRINT N'✓ Đã set DoiNhaId, DoiKhachId = NULL-able.';

    -- 3) Recreate FK (NoAction — bảo toàn hành vi cũ)
    ALTER TABLE [dbo].[TranDaus]
        ADD CONSTRAINT [FK_TranDaus_DoiBongs_DoiNhaId]
        FOREIGN KEY ([DoiNhaId]) REFERENCES [dbo].[DoiBongs]([Id])
        ON DELETE NO ACTION;

    ALTER TABLE [dbo].[TranDaus]
        ADD CONSTRAINT [FK_TranDaus_DoiBongs_DoiKhachId]
        FOREIGN KEY ([DoiKhachId]) REFERENCES [dbo].[DoiBongs]([Id])
        ON DELETE NO ACTION;
    PRINT N'✓ Đã tái tạo 2 FK.';

    -- 4) (Optional) Dọn dữ liệu placeholder cũ (nếu có bản ghi DoiNhaId = 0
    --    do KnockOutService bản trước từng insert lỗi giữa chừng).
    UPDATE [dbo].[TranDaus] SET [DoiNhaId]   = NULL WHERE [DoiNhaId]   = 0;
    UPDATE [dbo].[TranDaus] SET [DoiKhachId] = NULL WHERE [DoiKhachId] = 0;
END
ELSE
BEGIN
    PRINT N'ℹ️  DoiNhaId / DoiKhachId đã NULL-able — bỏ qua.';
END

-- ── Ghi __EFMigrationsHistory để `dotnet ef database update` không apply lại ─
IF NOT EXISTS (
    SELECT 1 FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260703113000_AllowNullDoiNhaDoiKhachInTranDau'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260703113000_AllowNullDoiNhaDoiKhachInTranDau', N'8.0.25');
    PRINT N'✓ Đã ghi __EFMigrationsHistory.';
END

COMMIT TRANSACTION;
GO

-- ── Rollback (dán riêng khi cần gỡ) ────────────────────────────────
-- ALTER TABLE [dbo].[TranDaus] DROP CONSTRAINT [FK_TranDaus_DoiBongs_DoiNhaId];
-- ALTER TABLE [dbo].[TranDaus] DROP CONSTRAINT [FK_TranDaus_DoiBongs_DoiKhachId];
-- UPDATE [dbo].[TranDaus] SET DoiNhaId   = 0 WHERE DoiNhaId   IS NULL;  -- ← nguy hiểm nếu có FK khác
-- UPDATE [dbo].[TranDaus] SET DoiKhachId = 0 WHERE DoiKhachId IS NULL;
-- ALTER TABLE [dbo].[TranDaus] ALTER COLUMN [DoiNhaId]   INT NOT NULL;
-- ALTER TABLE [dbo].[TranDaus] ALTER COLUMN [DoiKhachId] INT NOT NULL;
-- ALTER TABLE [dbo].[TranDaus]
--     ADD CONSTRAINT [FK_TranDaus_DoiBongs_DoiNhaId]
--     FOREIGN KEY ([DoiNhaId])   REFERENCES [dbo].[DoiBongs]([Id]) ON DELETE NO ACTION;
-- ALTER TABLE [dbo].[TranDaus]
--     ADD CONSTRAINT [FK_TranDaus_DoiBongs_DoiKhachId]
--     FOREIGN KEY ([DoiKhachId]) REFERENCES [dbo].[DoiBongs]([Id]) ON DELETE NO ACTION;
-- DELETE FROM [dbo].[__EFMigrationsHistory]
--   WHERE [MigrationId] = N'20260703113000_AllowNullDoiNhaDoiKhachInTranDau';
