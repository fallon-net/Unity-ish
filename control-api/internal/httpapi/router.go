package httpapi

import (
	"database/sql"
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"strings"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"unity-ish/control-api/internal/config"
	"unity-ish/control-api/internal/token"
)

// knownChannels defines the two party-lines in MVP.
var knownChannels = []string{"PL-A", "PL-B"}

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

// handleHealth returns a simple liveness probe.
func (r *Router) handleHealth(w http.ResponseWriter, _ *http.Request) {
	writeJSON(w, http.StatusOK, map[string]any{
		"status": "ok",
		"time":   time.Now().UTC().Format(time.RFC3339),
	})
}

// handleLogin validates local credentials and issues a short-lived app JWT.
// TODO: compare password against bcrypt hash stored in SQLite (plain-text stub for dev).
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

// liveKitTokenRequest describes which channels the client wants tokens for
// and which ones it is allowed to publish (talk) on.
type liveKitTokenRequest struct {
	Participant string   `json:"participant"`
	Channels    []string `json:"channels"`
	CanTalk     []string `json:"canTalk"`
}

// handleLiveKitToken issues signed LiveKit room tokens for each requested channel.
// Requires a valid app Bearer token from /v1/auth/login.
func (r *Router) handleLiveKitToken(w http.ResponseWriter, req *http.Request) {
	if req.Method != http.MethodPost {
		w.WriteHeader(http.StatusMethodNotAllowed)
		return
	}

	claims, err := r.verifyBearer(req)
	if err != nil {
		writeJSON(w, http.StatusUnauthorized, map[string]string{"error": "unauthorized"})
		return
	}

	var in liveKitTokenRequest
	if err := json.NewDecoder(req.Body).Decode(&in); err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "invalid json"})
		return
	}

	// Use the identity from the app token when the body omits it.
	if in.Participant == "" {
		in.Participant, _ = claims["sub"].(string)
	}
	if in.Participant == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "participant required"})
		return
	}

	// Default to all channels if caller omits the list.
	if len(in.Channels) == 0 {
		in.Channels = knownChannels
	}

	// Build a set of channels where talking is permitted.
	canTalkSet := make(map[string]bool, len(in.CanTalk))
	for _, ch := range in.CanTalk {
		canTalkSet[ch] = true
	}

	tokens := make(map[string]string, len(in.Channels))
	for _, ch := range in.Channels {
		room := token.RoomForChannel(ch)
		lkToken, err := token.NewLiveKitToken(token.LiveKitTokenParams{
			APIKey:       r.cfg.LiveKitAPIKey,
			APISecret:    r.cfg.LiveKitAPISecret,
			Room:         room,
			Participant:  fmt.Sprintf("%s-%s", in.Participant, strings.ToLower(ch)),
			CanPublish:   canTalkSet[ch],
			CanSubscribe: true,
			TTL:          5 * time.Minute,
		})
		if err != nil {
			writeJSON(w, http.StatusInternalServerError, map[string]string{"error": "token generation failed"})
			return
		}
		tokens[ch] = lkToken
	}

	writeJSON(w, http.StatusOK, map[string]any{
		"livekitUrl": r.cfg.LiveKitURL,
		"tokens":     tokens,
	})
}

// verifyBearer validates the Authorization: Bearer <token> header against the
// local JWT signing key and returns the claims on success.
func (r *Router) verifyBearer(req *http.Request) (jwt.MapClaims, error) {
	header := req.Header.Get("Authorization")
	if !strings.HasPrefix(header, "Bearer ") {
		return nil, errors.New("missing bearer token")
	}
	raw := strings.TrimPrefix(header, "Bearer ")

	tok, err := jwt.Parse(raw, func(t *jwt.Token) (any, error) {
		if _, ok := t.Method.(*jwt.SigningMethodHMAC); !ok {
			return nil, fmt.Errorf("unexpected signing method: %v", t.Header["alg"])
		}
		return []byte(r.cfg.JWTSigningKey), nil
	})
	if err != nil || !tok.Valid {
		return nil, errors.New("invalid token")
	}

	claims, ok := tok.Claims.(jwt.MapClaims)
	if !ok {
		return nil, errors.New("malformed claims")
	}
	return claims, nil
}

func writeJSON(w http.ResponseWriter, code int, payload any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(code)
	_ = json.NewEncoder(w).Encode(payload)
}
