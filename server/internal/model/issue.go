// Package model is the shared inspection issue shape.
// JSON field names are the contract used by Unity and the React admin.
package model

import "strings"

const (
	PriorityLow      = "low"
	PriorityMedium   = "medium"
	PriorityHigh     = "high"
	StatusOpen       = "open"
	StatusInProgress = "in_progress"
	StatusResolved   = "resolved"
)

// Position is the marker pose in the AR session, in meters.
type Position struct {
	X float64 `json:"x"`
	Y float64 `json:"y"`
	Z float64 `json:"z"`
}

// Issue is one on-site inspection report.
type Issue struct {
	ID          string   `json:"id"`
	Title       string   `json:"title"`
	Description string   `json:"description"`
	Priority    string   `json:"priority"`
	Status      string   `json:"status"`
	Position    Position `json:"position"`
	CreatedAt   string   `json:"createdAt"`
	UpdatedAt   string   `json:"updatedAt"`
}

// NormalizeToken trims and lowercases an enum value.
func NormalizeToken(v string) string {
	return strings.ToLower(strings.TrimSpace(v))
}

// ValidPriority reports whether v is low, medium, or high.
func ValidPriority(v string) bool {
	switch v {
	case PriorityLow, PriorityMedium, PriorityHigh:
		return true
	default:
		return false
	}
}

// ValidStatus reports whether v is open, in_progress, or resolved.
func ValidStatus(v string) bool {
	switch v {
	case StatusOpen, StatusInProgress, StatusResolved:
		return true
	default:
		return false
	}
}
