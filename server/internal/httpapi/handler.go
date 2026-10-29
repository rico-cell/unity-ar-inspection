// Package httpapi serves the inspection issue API used by Unity and React.
package httpapi

import (
	"context"
	"encoding/json"
	"errors"
	"io"
	"log"
	"math"
	"net/http"
	"strings"
	"unicode/utf8"

	"inspection/server/internal/model"
	"inspection/server/internal/store"
)

// NewHandler returns the HTTP handler, including CORS.
func NewHandler(st *store.Store) http.Handler {
	h := &handler{store: st}
	mux := http.NewServeMux()
	mux.HandleFunc("GET /health", h.health)
	mux.HandleFunc("GET /api/issues", h.list)
	mux.HandleFunc("POST /api/issues", h.create)
	mux.HandleFunc("GET /api/issues/{id}", h.get)
	mux.HandleFunc("PATCH /api/issues/{id}", h.patchStatus)
	mux.HandleFunc("DELETE /api/issues/{id}", h.delete)
	mux.HandleFunc("GET /", h.root)
	return cors(mux)
}

type handler struct {
	store *store.Store
}

type createRequest struct {
	Title       *string         `json:"title"`
	Description *string         `json:"description"`
	Priority    *string         `json:"priority"`
	Position    *model.Position `json:"position"`
}

type statusRequest struct {
	Status *string `json:"status"`
}

type issueListResponse struct {
	Issues []model.Issue `json:"issues"`
}

func (h *handler) root(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path != "/" {
		writeError(w, http.StatusNotFound, "not found")
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{
		"service": "inspection-api",
		"health":  "/health",
		"issues":  "/api/issues",
	})
}

func (h *handler) health(w http.ResponseWriter, r *http.Request) {
	if err := h.store.Ping(r.Context()); err != nil {
		log.Printf("health: %v", err)
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{
			"status": "error",
			"error":  "database unavailable",
		})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"status": "ok"})
}

func (h *handler) list(w http.ResponseWriter, r *http.Request) {
	issues, err := h.store.List(r.Context())
	if err != nil {
		log.Printf("list issues: %v", err)
		writeError(w, http.StatusInternalServerError, "internal error")
		return
	}
	if issues == nil {
		issues = []model.Issue{}
	}
	writeJSON(w, http.StatusOK, issueListResponse{Issues: issues})
}

func (h *handler) create(w http.ResponseWriter, r *http.Request) {
	var req createRequest
	if err := decodeJSON(r, &req); err != nil {
		writeError(w, http.StatusBadRequest, "invalid JSON")
		return
	}
	title, description, priority, pos, msg := validateCreate(req)
	if msg != "" {
		writeError(w, http.StatusBadRequest, msg)
		return
	}
	issue, err := h.store.Create(r.Context(), title, description, priority, pos)
	if err != nil {
		log.Printf("create issue: %v", err)
		writeError(w, http.StatusInternalServerError, "internal error")
		return
	}
	writeJSON(w, http.StatusCreated, issue)
}

func (h *handler) get(w http.ResponseWriter, r *http.Request) {
	issue, err := h.store.Get(r.Context(), r.PathValue("id"))
	if err != nil {
		writeStoreError(w, err)
		return
	}
	writeJSON(w, http.StatusOK, issue)
}

func (h *handler) patchStatus(w http.ResponseWriter, r *http.Request) {
	var req statusRequest
	if err := decodeJSON(r, &req); err != nil {
		writeError(w, http.StatusBadRequest, "invalid JSON")
		return
	}
	status := model.NormalizeToken(deref(req.Status))
	if !model.ValidStatus(status) {
		writeError(w, http.StatusBadRequest, "status must be open, in_progress, or resolved")
		return
	}
	issue, err := h.store.UpdateStatus(r.Context(), r.PathValue("id"), status)
	if err != nil {
		writeStoreError(w, err)
		return
	}
	writeJSON(w, http.StatusOK, issue)
}

func (h *handler) delete(w http.ResponseWriter, r *http.Request) {
	if err := h.store.Delete(r.Context(), r.PathValue("id")); err != nil {
		writeStoreError(w, err)
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func validateCreate(req createRequest) (title, description, priority string, pos model.Position, msg string) {
	title = strings.TrimSpace(deref(req.Title))
	if title == "" {
		return "", "", "", model.Position{}, "title is required"
	}
	if utf8.RuneCountInString(title) > 200 {
		return "", "", "", model.Position{}, "title must be at most 200 characters"
	}
	description = strings.TrimSpace(deref(req.Description))
	if utf8.RuneCountInString(description) > 4000 {
		return "", "", "", model.Position{}, "description must be at most 4000 characters"
	}
	priority = model.NormalizeToken(deref(req.Priority))
	if !model.ValidPriority(priority) {
		return "", "", "", model.Position{}, "priority must be low, medium, or high"
	}
	if req.Position == nil {
		return "", "", "", model.Position{}, "position is required"
	}
	if !finite(req.Position.X) || !finite(req.Position.Y) || !finite(req.Position.Z) {
		return "", "", "", model.Position{}, "position x, y, and z must be finite numbers"
	}
	return title, description, priority, *req.Position, ""
}

func finite(v float64) bool {
	return !math.IsNaN(v) && !math.IsInf(v, 0)
}

func deref(s *string) string {
	if s == nil {
		return ""
	}
	return *s
}

func decodeJSON(r *http.Request, dest any) error {
	defer r.Body.Close()
	// Unknown fields are ignored so a client can send id/status/timestamps
	// without failing. The server still generates those values itself.
	dec := json.NewDecoder(r.Body)
	if err := dec.Decode(dest); err != nil {
		return err
	}
	if err := dec.Decode(&struct{}{}); err != io.EOF {
		return errors.New("extra JSON")
	}
	return nil
}

func writeStoreError(w http.ResponseWriter, err error) {
	if errors.Is(err, store.ErrNotFound) {
		writeError(w, http.StatusNotFound, "issue not found")
		return
	}
	if errors.Is(err, context.Canceled) {
		writeError(w, http.StatusRequestTimeout, "request canceled")
		return
	}
	log.Printf("store: %v", err)
	writeError(w, http.StatusInternalServerError, "internal error")
}

func writeError(w http.ResponseWriter, status int, message string) {
	writeJSON(w, status, map[string]string{"error": message})
}

func writeJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	enc := json.NewEncoder(w)
	enc.SetEscapeHTML(false)
	if err := enc.Encode(v); err != nil {
		log.Printf("write json: %v", err)
	}
}

func cors(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Access-Control-Allow-Origin", "*")
		w.Header().Set("Access-Control-Allow-Methods", "GET, POST, PATCH, DELETE, OPTIONS")
		w.Header().Set("Access-Control-Allow-Headers", "Content-Type, Accept")
		w.Header().Set("Access-Control-Max-Age", "600")
		if r.Method == http.MethodOptions {
			w.WriteHeader(http.StatusNoContent)
			return
		}
		next.ServeHTTP(w, r)
	})
}
