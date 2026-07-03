package checkin

import (
	"context"
	"errors"
	"net/http"
	"time"

	"gostaffapi/internal/models"
)

// LUU Y: da xoa CheckInBooking/ErrBookingNotFound/ErrBookingState (check-in dat
// san thuong) — tinh nang nay da co san trong C# (StaffController.ThucHienCheckIn)
// va java-recommendation (CheckInService.java). Go chi con giu "diem danh cau
// thu giai dau" — tinh nang MOI, chua ton tai o dau khac.
var (
	ErrForbidden       = errors.New("staff is not assigned to this stadium")
	ErrMatchNotFound   = errors.New("match not found")
	ErrMatchClosed     = errors.New("match is closed")
	ErrPlayerNotInGame = errors.New("player does not belong to this match")
	ErrPlayerSuspended = errors.New("player is suspended")
)

type Store interface {
	GetCheckIns(ctx context.Context, matchID int, staffID int) (models.CheckInList, error)
	TogglePlayer(ctx context.Context, matchID int, playerID int, staffID int) (models.ToggleResult, error)
	DemoStatus(ctx context.Context, startedAt time.Time) (models.DemoStatus, error)
}

type Service struct {
	store Store
}

func NewService(store Store) *Service {
	return &Service{store: store}
}

func (s *Service) GetCheckIns(ctx context.Context, matchID int, staffID int) (models.CheckInList, error) {
	return s.store.GetCheckIns(ctx, matchID, staffID)
}

func (s *Service) TogglePlayer(ctx context.Context, matchID int, playerID int, staffID int) (models.ToggleResult, error) {
	return s.store.TogglePlayer(ctx, matchID, playerID, staffID)
}

func (s *Service) DemoStatus(ctx context.Context, startedAt time.Time) (models.DemoStatus, error) {
	return s.store.DemoStatus(ctx, startedAt)
}

func StatusCode(err error) int {
	switch {
	case errors.Is(err, ErrForbidden):
		return http.StatusForbidden
	case errors.Is(err, ErrMatchNotFound), errors.Is(err, ErrPlayerNotInGame):
		return http.StatusNotFound
	case errors.Is(err, ErrMatchClosed), errors.Is(err, ErrPlayerSuspended):
		return http.StatusBadRequest
	default:
		return http.StatusInternalServerError
	}
}
