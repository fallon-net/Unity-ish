package main

import (
	"log"
	"net/http"

	"unity-ish/control-api/internal/config"
	"unity-ish/control-api/internal/httpapi"
	"unity-ish/control-api/internal/store"
)

func main() {
	cfg, err := config.Load()
	if err != nil {
		log.Fatalf("failed to load config: %v", err)
	}

	db, err := store.OpenSQLite(cfg.SQLitePath)
	if err != nil {
		log.Fatalf("failed to open sqlite: %v", err)
	}
	defer db.Close()

	if err := store.EnsureSchema(db); err != nil {
		log.Fatalf("failed to ensure schema: %v", err)
	}

	handler := httpapi.NewRouter(cfg, db)
	addr := ":" + cfg.Port
	log.Printf("control-api listening on %s", addr)
	if err := http.ListenAndServe(addr, handler); err != nil {
		log.Fatalf("server stopped: %v", err)
	}
}
