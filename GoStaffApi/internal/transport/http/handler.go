package http

import (
	"context"
	"log"
	nethttp "net/http"
	"strconv"
	"time"

	"github.com/gin-gonic/gin"

	"gostaffapi/internal/business/checkin"
	"gostaffapi/internal/middleware"
)

// LUU Y: da xoa checkInBooking() (POST /api/staff/bookings/:bookingId/check-in)
// — trung chuc nang voi C# StaffController.ThucHienCheckIn() va
// java-recommendation CheckInService.java (3 noi cung lam 1 viec). Chi con giu
// 2 endpoint diem danh cau thu giai dau — tinh nang MOI — nay da doi tu
// X-Internal-Api-Key + X-Staff-Id (khong an toan, tin thang client) sang
// JWTMiddleware (doc cookie "jwt"/Authorization header, verify chu ky, lay
// staffId that tu claim UserId).

type Handler struct {
	service   *checkin.Service
	jwtSecret string
	started   time.Time
}

func NewHandler(service *checkin.Service, jwtSecret string, started time.Time) *Handler {
	return &Handler{service: service, jwtSecret: jwtSecret, started: started}
}

func (h *Handler) Register(router *gin.Engine) {
	router.GET("/health", func(c *gin.Context) {
		c.JSON(nethttp.StatusOK, gin.H{"ok": true, "service": "Go Staff Check-in API"})
	})

	router.GET("/api/demo/status", h.demoStatus)

	jwtAuth := middleware.JWTMiddleware(h.jwtSecret)
	router.GET("/api/tournament/matches/:matchId/checkins", jwtAuth, h.getCheckIns)
	router.POST("/api/tournament/matches/:matchId/checkins/:playerId/toggle", jwtAuth, h.togglePlayer)
}

func (h *Handler) getCheckIns(c *gin.Context) {
	matchID, ok := intParam(c, "matchId")
	if !ok {
		return
	}
	staffID := c.GetInt(middleware.ContextStaffID)

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
	staffID := c.GetInt(middleware.ContextStaffID)

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
