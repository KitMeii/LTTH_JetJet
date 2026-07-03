package sqlserver

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"time"

	"gostaffapi/internal/business/checkin"
	"gostaffapi/internal/models"
)

type Store struct {
	db *sql.DB
}

func NewStore(db *sql.DB) *Store {
	return &Store{db: db}
}

func (s *Store) EnsureSchema(ctx context.Context) error {
	query := `
IF OBJECT_ID('dbo.TournamentPlayerCheckIns', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TournamentPlayerCheckIns (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TranDauId INT NOT NULL,
        ThanhVienDoiId INT NOT NULL,
        StaffId INT NOT NULL,
        IsCheckedIn BIT NOT NULL CONSTRAINT DF_TournamentPlayerCheckIns_IsCheckedIn DEFAULT 1,
        CheckedAt DATETIME NOT NULL CONSTRAINT DF_TournamentPlayerCheckIns_CheckedAt DEFAULT GETDATE()
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_TournamentPlayerCheckIns_TranDau_ThanhVien'
      AND object_id = OBJECT_ID('dbo.TournamentPlayerCheckIns')
)
BEGIN
    CREATE UNIQUE INDEX UX_TournamentPlayerCheckIns_TranDau_ThanhVien
    ON dbo.TournamentPlayerCheckIns (TranDauId, ThanhVienDoiId);
END;`
	_, err := s.db.ExecContext(ctx, query)
	return err
}

func (s *Store) GetCheckIns(ctx context.Context, matchID int, staffID int) (models.CheckInList, error) {
	if err := s.validateMatchAccess(ctx, matchID, nil, staffID); err != nil {
		return models.CheckInList{}, err
	}

	rows, err := s.db.QueryContext(ctx, `
SELECT ThanhVienDoiId
FROM dbo.TournamentPlayerCheckIns
WHERE TranDauId = @matchId AND IsCheckedIn = 1
ORDER BY ThanhVienDoiId`,
		sql.Named("matchId", matchID),
	)
	if err != nil {
		return models.CheckInList{}, err
	}
	defer rows.Close()

	ids := make([]int, 0)
	for rows.Next() {
		var id int
		if err := rows.Scan(&id); err != nil {
			return models.CheckInList{}, err
		}
		ids = append(ids, id)
	}
	if err := rows.Err(); err != nil {
		return models.CheckInList{}, err
	}

	return models.CheckInList{
		MatchID:          matchID,
		CheckedPlayerIDs: ids,
		TotalChecked:     len(ids),
		SyncedAt:         time.Now(),
	}, nil
}

func (s *Store) TogglePlayer(ctx context.Context, matchID int, playerID int, staffID int) (models.ToggleResult, error) {
	if err := s.validateMatchAccess(ctx, matchID, &playerID, staffID); err != nil {
		return models.ToggleResult{}, err
	}

	tx, err := s.db.BeginTx(ctx, nil)
	if err != nil {
		return models.ToggleResult{}, err
	}
	defer tx.Rollback()

	var current sql.NullBool
	err = tx.QueryRowContext(ctx, `
SELECT IsCheckedIn
FROM dbo.TournamentPlayerCheckIns WITH (UPDLOCK, HOLDLOCK)
WHERE TranDauId = @matchId AND ThanhVienDoiId = @playerId`,
		sql.Named("matchId", matchID),
		sql.Named("playerId", playerID),
	).Scan(&current)

	checked := true
	switch {
	case errors.Is(err, sql.ErrNoRows):
		_, err = tx.ExecContext(ctx, `
INSERT INTO dbo.TournamentPlayerCheckIns (TranDauId, ThanhVienDoiId, StaffId, IsCheckedIn, CheckedAt)
VALUES (@matchId, @playerId, @staffId, 1, GETDATE())`,
			sql.Named("matchId", matchID),
			sql.Named("playerId", playerID),
			sql.Named("staffId", staffID),
		)
		if err != nil {
			return models.ToggleResult{}, err
		}
	case err != nil:
		return models.ToggleResult{}, err
	default:
		checked = !current.Bool
		_, err = tx.ExecContext(ctx, `
UPDATE dbo.TournamentPlayerCheckIns
SET StaffId = @staffId, IsCheckedIn = @checked, CheckedAt = GETDATE()
WHERE TranDauId = @matchId AND ThanhVienDoiId = @playerId`,
			sql.Named("matchId", matchID),
			sql.Named("playerId", playerID),
			sql.Named("staffId", staffID),
			sql.Named("checked", checked),
		)
		if err != nil {
			return models.ToggleResult{}, err
		}
	}

	var total int
	if err := tx.QueryRowContext(ctx, `
SELECT COUNT(1)
FROM dbo.TournamentPlayerCheckIns
WHERE TranDauId = @matchId AND IsCheckedIn = 1`,
		sql.Named("matchId", matchID),
	).Scan(&total); err != nil {
		return models.ToggleResult{}, err
	}

	if err := tx.Commit(); err != nil {
		return models.ToggleResult{}, err
	}

	action := "unchecked"
	if checked {
		action = "checked"
	}

	return models.ToggleResult{
		OK:           true,
		MatchID:      matchID,
		PlayerID:     playerID,
		Checked:      checked,
		TotalChecked: total,
		Message:      fmt.Sprintf("player %s", action),
		SyncedAt:     time.Now(),
	}, nil
}

func (s *Store) CheckInBooking(ctx context.Context, bookingID int, staffID int) (models.BookingCheckInResult, error) {
	tx, err := s.db.BeginTx(ctx, nil)
	if err != nil {
		return models.BookingCheckInResult{}, err
	}
	defer tx.Rollback()

	var status string
	var sanBongID int
	var confirmation string
	var customerName string
	var stadiumName string

	err = tx.QueryRowContext(ctx, `
SELECT d.TrangThai, k.SanBongId, d.MaXacNhan, ISNULL(u.HoTen, ''), ISNULL(s.TenSan, '')
FROM dbo.DatSans d WITH (UPDLOCK, HOLDLOCK)
JOIN dbo.KhungGios k ON k.Id = d.KhungGioId
JOIN dbo.SanBongs s ON s.Id = k.SanBongId
JOIN dbo.Users u ON u.Id = d.UserId
WHERE d.Id = @bookingId
  AND EXISTS (
      SELECT 1
      FROM dbo.StaffSanPhanCong p
      WHERE p.StaffId = @staffId AND p.SanBongId = k.SanBongId
  )`,
		sql.Named("bookingId", bookingID),
		sql.Named("staffId", staffID),
	).Scan(&status, &sanBongID, &confirmation, &customerName, &stadiumName)
	if errors.Is(err, sql.ErrNoRows) {
		return models.BookingCheckInResult{}, checkin.ErrBookingNotFound
	}
	if err != nil {
		return models.BookingCheckInResult{}, err
	}
	if status != "DaXacNhan" {
		return models.BookingCheckInResult{}, fmt.Errorf("%w: current status is %s", checkin.ErrBookingState, status)
	}

	if _, err := tx.ExecContext(ctx, `
UPDATE dbo.DatSans
SET TrangThai = 'DangSuDung', StaffCheckInId = @staffId
WHERE Id = @bookingId`,
		sql.Named("bookingId", bookingID),
		sql.Named("staffId", staffID),
	); err != nil {
		return models.BookingCheckInResult{}, err
	}

	if _, err := tx.ExecContext(ctx, `
UPDATE dv
SET TonKho =
    CASE
        WHEN dv.TonKho - dsdv.SoLuong < 0 THEN 0
        ELSE dv.TonKho - dsdv.SoLuong
    END
FROM dbo.DichVus dv
JOIN dbo.DatSan_DichVus dsdv ON dsdv.DichVuId = dv.Id
WHERE dsdv.DatSanId = @bookingId`,
		sql.Named("bookingId", bookingID),
	); err != nil {
		return models.BookingCheckInResult{}, err
	}

	if _, err := tx.ExecContext(ctx, `
INSERT INTO dbo.AuditLogs (UserId, VaiTro, HanhDong, DoiTuong, DoiTuongId, MoTa, ThoiGian)
VALUES (@staffId, 'Staff', 'CheckIn', 'DatSan', @bookingId, @message, GETDATE())`,
		sql.Named("staffId", staffID),
		sql.Named("bookingId", bookingID),
		sql.Named("message", fmt.Sprintf("Go API check-in booking %s - %s", confirmation, customerName)),
	); err != nil {
		return models.BookingCheckInResult{}, err
	}

	if err := tx.Commit(); err != nil {
		return models.BookingCheckInResult{}, err
	}

	return models.BookingCheckInResult{
		OK:           true,
		BookingID:    bookingID,
		StaffID:      staffID,
		StadiumID:    sanBongID,
		Confirmation: confirmation,
		CustomerName: customerName,
		StadiumName:  stadiumName,
		CheckedIn:    true,
		Message:      "booking checked in by Go API",
		SyncedAt:     time.Now(),
	}, nil
}

func (s *Store) DemoStatus(ctx context.Context, startedAt time.Time) (models.DemoStatus, error) {
	status := "connected"
	if err := s.db.PingContext(ctx); err != nil {
		status = "disconnected: " + err.Error()
	}

	var total int
	var checked int
	_ = s.db.QueryRowContext(ctx, `SELECT COUNT(1) FROM dbo.TournamentPlayerCheckIns`).Scan(&total)
	_ = s.db.QueryRowContext(ctx, `SELECT COUNT(1) FROM dbo.TournamentPlayerCheckIns WHERE IsCheckedIn = 1`).Scan(&checked)

	return models.DemoStatus{
		ServiceName:         "Go Staff Check-in API",
		UptimeSeconds:       int64(time.Since(startedAt).Seconds()),
		DBStatus:            status,
		TotalCheckInRecords: total,
		CheckedRecords:      checked,
		GeneratedAt:         time.Now(),
	}, nil
}

func (s *Store) validateMatchAccess(ctx context.Context, matchID int, playerID *int, staffID int) error {
	var status string
	var sanBongID int
	var playerExists int

	if playerID == nil {
		err := s.db.QueryRowContext(ctx, `
SELECT t.TrangThai, g.SanBongId, 1
FROM dbo.TranDaus t
JOIN dbo.GiaiDaus g ON g.Id = t.GiaiDauId
WHERE t.Id = @matchId`,
			sql.Named("matchId", matchID),
		).Scan(&status, &sanBongID, &playerExists)
		if errors.Is(err, sql.ErrNoRows) {
			return checkin.ErrMatchNotFound
		}
		if err != nil {
			return err
		}
	} else {
		err := s.db.QueryRowContext(ctx, `
SELECT t.TrangThai, g.SanBongId, tv.SoTranTreoGio
FROM dbo.TranDaus t
JOIN dbo.GiaiDaus g ON g.Id = t.GiaiDauId
JOIN dbo.ThanhVienDois tv ON tv.Id = @playerId
WHERE t.Id = @matchId
  AND (tv.DoiId = t.DoiNhaId OR tv.DoiId = t.DoiKhachId)`,
			sql.Named("matchId", matchID),
			sql.Named("playerId", *playerID),
		).Scan(&status, &sanBongID, &playerExists)
		if errors.Is(err, sql.ErrNoRows) {
			if exists, exErr := s.matchExists(ctx, matchID); exErr == nil && exists {
				return checkin.ErrPlayerNotInGame
			}
			return checkin.ErrMatchNotFound
		}
		if err != nil {
			return err
		}
		if playerExists > 0 {
			return checkin.ErrPlayerSuspended
		}
	}

	if status == "Closed" {
		return checkin.ErrMatchClosed
	}

	var allowed int
	err := s.db.QueryRowContext(ctx, `
SELECT COUNT(1)
FROM dbo.StaffSanPhanCong
WHERE StaffId = @staffId AND SanBongId = @sanBongId`,
		sql.Named("staffId", staffID),
		sql.Named("sanBongId", sanBongID),
	).Scan(&allowed)
	if err != nil {
		return err
	}
	if allowed == 0 {
		return checkin.ErrForbidden
	}

	return nil
}

func (s *Store) matchExists(ctx context.Context, matchID int) (bool, error) {
	var count int
	err := s.db.QueryRowContext(ctx, `SELECT COUNT(1) FROM dbo.TranDaus WHERE Id = @matchId`,
		sql.Named("matchId", matchID),
	).Scan(&count)
	return count > 0, err
}
