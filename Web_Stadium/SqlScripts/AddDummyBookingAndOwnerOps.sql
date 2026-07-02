BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601090612_AddDummyBookingAndOwnerOps'
)
BEGIN
    ALTER TABLE [GiaiDaus] ADD [LichBlockJson] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601090612_AddDummyBookingAndOwnerOps'
)
BEGIN
    ALTER TABLE [GiaiDaus] ADD [StaffPhuTrachId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601090612_AddDummyBookingAndOwnerOps'
)
BEGIN
    ALTER TABLE [DatSans] ADD [GiaiDauId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601090612_AddDummyBookingAndOwnerOps'
)
BEGIN
    ALTER TABLE [DatSans] ADD [LaDummyBooking] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601090612_AddDummyBookingAndOwnerOps'
)
BEGIN
    CREATE INDEX [IX_GiaiDaus_StaffPhuTrachId] ON [GiaiDaus] ([StaffPhuTrachId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601090612_AddDummyBookingAndOwnerOps'
)
BEGIN
    CREATE INDEX [IX_DatSans_GiaiDauId] ON [DatSans] ([GiaiDauId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601090612_AddDummyBookingAndOwnerOps'
)
BEGIN
    ALTER TABLE [DatSans] ADD CONSTRAINT [FK_DatSans_GiaiDaus_GiaiDauId] FOREIGN KEY ([GiaiDauId]) REFERENCES [GiaiDaus] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601090612_AddDummyBookingAndOwnerOps'
)
BEGIN
    ALTER TABLE [GiaiDaus] ADD CONSTRAINT [FK_GiaiDaus_Users_StaffPhuTrachId] FOREIGN KEY ([StaffPhuTrachId]) REFERENCES [Users] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601090612_AddDummyBookingAndOwnerOps'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260601090612_AddDummyBookingAndOwnerOps', N'8.0.25');
END;
GO

COMMIT;
GO

