package httpapi

import (
	"database/sql"
	"encoding/json"
	"net/http"
	"time"

	"unity-ish/control-api/internal/config"
	"unity-ish/control-api/internal/token"
)

type Router struct {
	cfg config.Config
	db  *sql.DB
}

func NewRouter(cfg config.Config, db *sql.DB) http.Handler {
	r := &Router{cfg: cfg, db: db}
	mux := http.NewServeMux()
	mux.HandleFunc("/healthz", r.handleHealth)
	mux.HandleFunc("/v1/auth/login", r.handleLogin)
	mux.HandleFunc("/v1/token/livekit", r.handleLiveKitToken)
	return mux
}

func (r *Router) handleHealth(w http.ResponseWriter, _ *http.Request) {
	writeJSON(w, http.StatusOK, map[string]any{
		"status": "ok",
		"time":   time.Now().UTC().Format(time.RFC3339),
	})
}

type loginRequest struct {
	Username string `json:"username"`
	Password string `json:"password"`
}

func (r *Router) handleLogin(w http.ResponseWriter, req *http.Request) {
	if req.Method != http.MethodPost {
		w.WriteHeader(http.StatusMethodNotAllowed)
		return
	}

	var in loginRequest
	if err := json.NewDecoder(req.Body).Decode(&in); err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "invalid json"})
		return
	}

	if in.Username == "" || in.Password == "" {
		writeJSON(w, http.StatusUnauthorized, map[string]string{"error": "invalid credentials"})
		return
	}

	appToken, err := token.NewAppToken(r.cfg.JWTSigningKey, in.Username, "crew", 10*time.Minute)
	if err != nil {
		writeJSON(w, http.StatusInternalServerError, map[string]string{"error": "failed to issue token"})
		return
	}

	writeJSON(w, http.StatusOK, map[string]any{
		"accessToken": appToken,
		"expiresIn":   600,
		"role":        "crew",
	})
}

func (r *Router) handleLiveKitToken(w http.ResponseWriter, req *http.Request) {
	if req.Method != http.MethodPost {
		w.WriteHeader(http.StatusMethodNotAllowed)
		return
	}

	user := req.URL.Query().Get("user")
	if user == "" {
		user = "operator"
	}

	// Placeholder for real LiveKit grant generation.
	writeJSON(w, http.StatusOK, map[string]any{
		"participant": user,
		"room":        "unityish-main",
		"token":       "replace-with-livekit-jwt",
	})
}

func writeJSON(w http.ResponseWriter, code int, payload any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(code)
	_ = json.NewEncoder(w).Encode(payload)
}
