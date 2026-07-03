package main

import (
	"context"
	"database/sql"
	"log"
	"net/http"
	"os"
	"time"

	_ "github.com/denisenkom/go-mssqldb"
	"github.com/gin-gonic/gin"

	"gostaffapi/internal/business/checkin"
	"gostaffapi/internal/storage/sqlserver"
	apihttp "gostaffapi/internal/transport/http"
)

func main() {
	dbConn := os.Getenv("GO_STAFF_DB")
	if dbConn == "" {
		log.Fatal("GO_STAFF_DB is required. Example: server=TunKittt;database=SanBongBTL;user id=sa;password=***;TrustServerCertificate=true")
	}

	port := env("GO_STAFF_PORT", "8081")
	apiKey := env("GO_STAFF_API_KEY", "dev-demo-key")

	db, err := sql.Open("sqlserver", dbConn)
	if err != nil {
		log.Fatalf("open sqlserver: %v", err)
	}
	defer db.Close()

	db.SetMaxOpenConns(10)
	db.SetMaxIdleConns(5)
	db.SetConnMaxLifetime(30 * time.Minute)

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()
	if err := db.PingContext(ctx); err != nil {
		log.Fatalf("ping sqlserver: %v", err)
	}

	store := sqlserver.NewStore(db)
	if err := store.EnsureSchema(ctx); err != nil {
		log.Fatalf("ensure schema: %v", err)
	}

	service := checkin.NewService(store)
	handler := apihttp.NewHandler(service, apiKey, time.Now())

	router := gin.Default()
	handler.Register(router)

	addr := ":" + port
	log.Printf("Go Staff API listening on http://localhost%s", addr)
	if err := http.ListenAndServe(addr, router); err != nil {
		log.Fatal(err)
	}
}

func env(key, fallback string) string {
	value := os.Getenv(key)
	if value == "" {
		return fallback
	}
	return value
}
