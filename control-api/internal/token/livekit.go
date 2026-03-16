package token

import (
	"time"

	"github.com/livekit/protocol/auth"
)

// RoomForChannel returns the canonical LiveKit room name for a party-line channel.
func RoomForChannel(channel string) string {
	switch channel {
	case "PL-A":
		return "unityish-pl-a"
	case "PL-B":
		return "unityish-pl-b"
	default:
		return "unityish-" + channel
	}
}

// LiveKitTokenParams holds all parameters needed to mint a single-room token.
type LiveKitTokenParams struct {
	APIKey      string
	APISecret   string
	Room        string
	Participant string
	CanPublish  bool
	CanSubscribe bool
	TTL         time.Duration
}

// NewLiveKitToken mints a signed LiveKit access token for one room.
func NewLiveKitToken(p LiveKitTokenParams) (string, error) {
	canPub := p.CanPublish
	canSub := p.CanSubscribe

	at := auth.NewAccessToken(p.APIKey, p.APISecret)
	grant := &auth.VideoGrant{
		RoomJoin:     true,
		Room:         p.Room,
		CanPublish:   &canPub,
		CanSubscribe: &canSub,
	}
	at.AddGrant(grant).
		SetIdentity(p.Participant).
		SetValidFor(p.TTL)

	return at.ToJWT()
}
