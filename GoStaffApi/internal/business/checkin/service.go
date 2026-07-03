package checkin

import (
	"context"
	"errors"
	"net/http"
	"time"

	"gostaffapi/internal/models"
)

var (
	ErrForbidden       = errors.New("staff is not assigned to this stadium")
	ErrMatchNotFound   = errors.New("match not found")
	ErrMatchClosed     = errors.New("match is closed")
	ErrPlayerNotInGame = errors.New("player does not belong to this match")
	ErrPlayerSuspended = errors.New("player is suspended")
	ErrBookingNotFound = errors.New("booking not found or not assigned to this staff")
	ErrBookingState    = errors.New("booking is not in DaXacNhan state")
)

type Store interface {
	GetCheckIns(ctx context.Context, matchID int, staffID int) (models.CheckInList, error)
	TogglePlayer(ctx context.Context, matchID int, playerID int, staffID int) (models.ToggleResult, error)
	CheckInBooking(ctx context.Context, bookingID int, staffID int) (models.BookingCheckInResult, error)
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

func (s *Service) CheckInBooking(ctx context.Context, bookingID int, staffID int) (models.BookingCheckInResult, error) {
	return s.store.CheckInBooking(ctx, bookingID, staffID)
}

func (s *Service) DemoStatus(ctx context.Context, startedAt time.Time) (models.DemoStatus, error) {
	return s.store.DemoStatus(ctx, startedAt)
}

func StatusCode(err error) int {
	switch {
	case errors.Is(err, ErrForbidden):
		return http.StatusForbidden
	case errors.Is(err, ErrMatchNotFound), errors.Is(err, ErrPlayerNotInGame), errors.Is(err, ErrBookingNotFound):
		return http.StatusNotFound
	case errors.Is(err, ErrMatchClosed), errors.Is(err, ErrPlayerSuspended), errors.Is(err, ErrBookingState):
		return http.StatusBadRequest
	default:
		return http.StatusInternalServerError
	}
}
