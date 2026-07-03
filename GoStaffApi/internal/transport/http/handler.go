package http

import (
	"context"
	"log"
	nethttp "net/http"
	"strconv"
	"time"

	"github.com/gin-gonic/gin"

	"gostaffapi/internal/business/checkin"
)

type Handler struct {
	service *checkin.Service
	apiKey  string
	started time.Time
}

func NewHandler(service *checkin.Service, apiKey string, started time.Time) *Handler {
	return &Handler{service: service, apiKey: apiKey, started: started}
}

func (h *Handler) Register(router *gin.Engine) {
	router.GET("/health", func(c *gin.Context) {
		c.JSON(nethttp.StatusOK, gin.H{"ok": true, "service": "Go Staff Check-in API"})
	})

	router.GET("/api/demo/status", h.demoStatus)

	protected := router.Group("/api")
	protected.Use(h.requireAPIKey())
	protected.GET("/tournament/matches/:matchId/checkins", h.getCheckIns)
	protected.POST("/tournament/matches/:matchId/checkins/:playerId/toggle", h.togglePlayer)
	protected.POST("/staff/bookings/:bookingId/check-in", h.checkInBooking)
}

func (h *Handler) requireAPIKey() gin.HandlerFunc {
	return func(c *gin.Context) {
		if h.apiKey != "" && c.GetHeader("X-Internal-Api-Key") != h.apiKey {
			c.AbortWithStatusJSON(nethttp.StatusUnauthorized, gin.H{
				"ok":      false,
				"message": "missing or invalid X-Internal-Api-Key",
			})
			return
		}
		c.Next()
	}
}

func (h *Handler) getCheckIns(c *gin.Context) {
	matchID, ok := intParam(c, "matchId")
	if !ok {
		return
	}
	staffID, ok := staffID(c)
	if !ok {
		return
	}

	ctx, cancel := context.WithTimeout(c.Request.Context(), 5*time.Second)
	defer cancel()

	result, err := h.service.GetCheckIns(ctx, matchID, staffID)
	if err != nil {
		c.JSON(checkin.StatusCode(err), gin.H{"ok": false, "message": err.Error()})
		return
	}

	c.JSON(nethttp.StatusOK, result)
}

func (h *Handler) togglePlayer(c *gin.Context) {
	matchID, ok := intParam(c, "matchId")
	if !ok {
		return
	}
	playerID, ok := intParam(c, "playerId")
	if !ok {
		return
	}
	staffID, ok := staffID(c)
	if !ok {
		return
	}

	ctx, cancel := context.WithTimeout(c.Request.Context(), 5*time.Second)
	defer cancel()

	result, err := h.service.TogglePlayer(ctx, matchID, playerID, staffID)
	if err != nil {
		log.Printf("toggle failed staffId=%d matchId=%d playerId=%d error=%s", staffID, matchID, playerID, err)
		c.JSON(checkin.StatusCode(err), gin.H{"ok": false, "message": err.Error()})
		return
	}

	action := "unchecked"
	if result.Checked {
		action = "checked"
	}
	log.Printf("staffId=%d matchId=%d playerId=%d action=%s totalChecked=%d",
		staffID, matchID, playerID, action, result.TotalChecked)

	c.JSON(nethttp.StatusOK, result)
}

func (h *Handler) checkInBooking(c *gin.Context) {
	bookingID, ok := intParam(c, "bookingId")
	if !ok {
		return
	}
	staffID, ok := staffID(c)
	if !ok {
		return
	}

	ctx, cancel := context.WithTimeout(c.Request.Context(), 5*time.Second)
	defer cancel()

	result, err := h.service.CheckInBooking(ctx, bookingID, staffID)
	if err != nil {
		log.Printf("booking check-in failed staffId=%d bookingId=%d error=%s", staffID, bookingID, err)
		c.JSON(checkin.StatusCode(err), gin.H{"ok": false, "message": err.Error()})
		return
	}

	log.Printf("staffId=%d bookingId=%d stadiumId=%d action=booking_checked_in confirmation=%s",
		staffID, bookingID, result.StadiumID, result.Confirmation)

	c.JSON(nethttp.StatusOK, result)
}

func (h *Handler) demoStatus(c *gin.Context) {
	ctx, cancel := context.WithTimeout(c.Request.Context(), 5*time.Second)
	defer cancel()

	result, err := h.service.DemoStatus(ctx, h.started)
	if err != nil {
		c.JSON(nethttp.StatusInternalServerError, gin.H{"ok": false, "message": err.Error()})
		return
	}
	c.JSON(nethttp.StatusOK, result)
}

func intParam(c *gin.Context, name string) (int, bool) {
	value, err := strconv.Atoi(c.Param(name))
	if err != nil || value <= 0 {
		c.JSON(nethttp.StatusBadRequest, gin.H{"ok": false, "message": name + " is invalid"})
		return 0, false
	}
	return value, true
}

func staffID(c *gin.Context) (int, bool) {
	value, err := strconv.Atoi(c.GetHeader("X-Staff-Id"))
	if err != nil || value <= 0 {
		c.JSON(nethttp.StatusBadRequest, gin.H{"ok": false, "message": "missing or invalid X-Staff-Id"})
		return 0, false
	}
	return value, true
}
