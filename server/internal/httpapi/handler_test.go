package httpapi

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"strings"
	"testing"

	"inspection/server/internal/model"
	"inspection/server/internal/store"
)

func newTestHandler(t *testing.T) http.Handler {
	t.Helper()
	st, err := store.Open(filepath.Join(t.TempDir(), "issues.db"))
	if err != nil {
		t.Fatalf("open: %v", err)
	}
	t.Cleanup(func() { _ = st.Close() })
	return NewHandler(st)
}

func do(handler http.Handler, method, path, body string) *httptest.ResponseRecorder {
	var r *http.Request
	if body != "" {
		r = httptest.NewRequest(method, path, strings.NewReader(body))
		r.Header.Set("Content-Type", "application/json")
	} else {
		r = httptest.NewRequest(method, path, nil)
	}
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, r)
	return w
}

func decode(t *testing.T, w *httptest.ResponseRecorder, dest any) {
	t.Helper()
	if err := json.Unmarshal(w.Body.Bytes(), dest); err != nil {
		t.Fatalf("json %s: %v", w.Body.String(), err)
	}
}

func TestHealthAndCORS(t *testing.T) {
	h := newTestHandler(t)
	w := do(h, http.MethodGet, "/health", "")
	if w.Code != http.StatusOK {
		t.Fatalf("status %d body %s", w.Code, w.Body.String())
	}
	if got := w.Header().Get("Access-Control-Allow-Origin"); got != "*" {
		t.Fatalf("cors origin %q", got)
	}
	var body map[string]string
	decode(t, w, &body)
	if body["status"] != "ok" {
		t.Fatalf("health = %+v", body)
	}

	opt := do(h, http.MethodOptions, "/api/issues", "")
	if opt.Code != http.StatusNoContent {
		t.Fatalf("options %d", opt.Code)
	}
	if got := opt.Header().Get("Access-Control-Allow-Methods"); !strings.Contains(got, "PATCH") {
		t.Fatalf("allow methods %q", got)
	}
}

func TestCreateListUpdateDelete(t *testing.T) {
	h := newTestHandler(t)
	created := postIssue(t, h, `{
		"title":"  入口墙面破损  ",
		"description":"左侧墙体存在裂缝",
		"priority":"HIGH",
		"status":"resolved",
		"position":{"x":0.42,"y":0.03,"z":1.26}
	}`)
	if created.Title != "入口墙面破损" {
		t.Fatalf("title %q", created.Title)
	}
	if created.Priority != model.PriorityHigh {
		t.Fatalf("priority %s", created.Priority)
	}
	if created.Status != model.StatusOpen {
		t.Fatalf("client status was kept: %s", created.Status)
	}
	if !strings.HasPrefix(created.ID, "iss_") {
		t.Fatalf("id %s", created.ID)
	}

	listW := do(h, http.MethodGet, "/api/issues", "")
	if listW.Code != http.StatusOK {
		t.Fatalf("list %d %s", listW.Code, listW.Body.String())
	}
	var list struct {
		Issues []model.Issue `json:"issues"`
	}
	decode(t, listW, &list)
	if len(list.Issues) != 1 || list.Issues[0].ID != created.ID {
		t.Fatalf("list %+v", list.Issues)
	}

	patch := do(h, http.MethodPatch, "/api/issues/"+created.ID, `{"status":"in_progress","title":"should ignore"}`)
	if patch.Code != http.StatusOK {
		t.Fatalf("patch %d %s", patch.Code, patch.Body.String())
	}
	var updated model.Issue
	decode(t, patch, &updated)
	if updated.Status != model.StatusInProgress || updated.Title != created.Title {
		t.Fatalf("updated %+v", updated)
	}

	got := do(h, http.MethodGet, "/api/issues/"+created.ID, "")
	if got.Code != http.StatusOK {
		t.Fatalf("get %d", got.Code)
	}

	del := do(h, http.MethodDelete, "/api/issues/"+created.ID, "")
	if del.Code != http.StatusNoContent {
		t.Fatalf("delete %d %s", del.Code, del.Body.String())
	}
	missing := do(h, http.MethodGet, "/api/issues/"+created.ID, "")
	if missing.Code != http.StatusNotFound {
		t.Fatalf("after delete %d", missing.Code)
	}
}

func TestValidationErrors(t *testing.T) {
	h := newTestHandler(t)
	cases := []struct {
		name string
		body string
		want string
	}{
		{"empty title", `{"title":"   ","priority":"low","position":{"x":0,"y":0,"z":0}}`, "title is required"},
		{"bad priority", `{"title":"裂缝","priority":"urgent","position":{"x":0,"y":0,"z":0}}`, "priority must be low, medium, or high"},
		{"missing position", `{"title":"裂缝","priority":"low"}`, "position is required"},
		{"bad json", `{`, "invalid JSON"},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			w := do(h, http.MethodPost, "/api/issues", tc.body)
			if w.Code != http.StatusBadRequest {
				t.Fatalf("status %d body %s", w.Code, w.Body.String())
			}
			var body map[string]string
			decode(t, w, &body)
			if body["error"] != tc.want {
				t.Fatalf("error %q", body["error"])
			}
		})
	}

	created := postIssue(t, h, `{"title":"门框松动","priority":"low","position":{"x":1,"y":0,"z":2}}`)
	bad := do(h, http.MethodPatch, "/api/issues/"+created.ID, `{"status":"done"}`)
	if bad.Code != http.StatusBadRequest {
		t.Fatalf("bad status %d %s", bad.Code, bad.Body.String())
	}
	var body map[string]string
	decode(t, bad, &body)
	if body["error"] != "status must be open, in_progress, or resolved" {
		t.Fatalf("error %q", body["error"])
	}
	still := do(h, http.MethodGet, "/api/issues/"+created.ID, "")
	var issue model.Issue
	decode(t, still, &issue)
	if issue.Status != model.StatusOpen {
		t.Fatalf("status changed to %s", issue.Status)
	}

	missing := do(h, http.MethodPatch, "/api/issues/iss_missing", `{"status":"open"}`)
	if missing.Code != http.StatusNotFound {
		t.Fatalf("missing %d", missing.Code)
	}
}

func TestEmptyList(t *testing.T) {
	h := newTestHandler(t)
	w := do(h, http.MethodGet, "/api/issues", "")
	if w.Code != http.StatusOK {
		t.Fatalf("status %d", w.Code)
	}
	if strings.TrimSpace(w.Body.String()) != `{"issues":[]}` {
		t.Fatalf("body %s", w.Body.String())
	}
}

func postIssue(t *testing.T, h http.Handler, body string) model.Issue {
	t.Helper()
	w := do(h, http.MethodPost, "/api/issues", body)
	if w.Code != http.StatusCreated {
		t.Fatalf("create %d %s", w.Code, w.Body.String())
	}
	var issue model.Issue
	decode(t, w, &issue)
	return issue
}
