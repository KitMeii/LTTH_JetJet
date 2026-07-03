package models

import "time"

type CheckInList struct {
	MatchID          int       `json:"matchId"`
	CheckedPlayerIDs []int     `json:"checkedPlayerIds"`
	TotalChecked     int       `json:"totalChecked"`
	SyncedAt         time.Time `json:"syncedAt"`
}

type ToggleResult struct {
	OK           bool      `json:"ok"`
	MatchID      int       `json:"matchId"`
	PlayerID     int       `json:"playerId"`
	Checked      bool      `json:"checked"`
	TotalChecked int       `json:"totalChecked"`
	Message      string    `json:"message"`
	SyncedAt     time.Time `json:"syncedAt"`
}

type DemoStatus struct {
	ServiceName         string    `json:"serviceName"`
	UptimeSeconds       int64     `json:"uptimeSeconds"`
	DBStatus            string    `json:"dbStatus"`
	TotalCheckInRecords int       `json:"totalCheckInRecords"`
	CheckedRecords      int       `json:"checkedRecords"`
	GeneratedAt         time.Time `json:"generatedAt"`
}
