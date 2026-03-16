package config

import (
	"fmt"
	"os"

	"github.com/joho/godotenv"
)

type Config struct {
	Port             string
	JWTSigningKey    string
	LiveKitAPIKey    string
	LiveKitAPISecret string
	LiveKitURL       string
	SQLitePath       string
}

func Load() (Config, error) {
	_ = godotenv.Load()

	cfg := Config{
		Port:             getOrDefault("PORT", "8080"),
		JWTSigningKey:    getOrDefault("JWT_SIGNING_KEY", "replace-me"),
		LiveKitAPIKey:    getOrDefault("LIVEKIT_API_KEY", "devkey"),
		LiveKitAPISecret: getOrDefault("LIVEKIT_API_SECRET", "devsecret"),
		LiveKitURL:       getOrDefault("LIVEKIT_URL", "ws://localhost:7880"),
		SQLitePath:       getOrDefault("SQLITE_PATH", "./data/unityish.db"),
	}

	if cfg.Port == "" {
		return Config{}, fmt.Errorf("PORT is required")
	}

	return cfg, nil
}

func getOrDefault(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}
