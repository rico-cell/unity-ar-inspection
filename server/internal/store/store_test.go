package store

import (
	"context"
	"path/filepath"
	"testing"

	"inspection/server/internal/model"
)

func TestIssueSurvivesReopen(t *testing.T) {
	ctx := context.Background()
	path := filepath.Join(t.TempDir(), "issues.db")

	first, err := Open(path)
	if err != nil {
		t.Fatalf("open: %v", err)
	}
	created, err := first.Create(ctx, "入口墙面破损", "左侧墙体存在裂缝", model.PriorityHigh, model.Position{X: 0.42, Y: 0.03, Z: 1.26})
	if err != nil {
		t.Fatalf("create: %v", err)
	}
	if created.Status != model.StatusOpen {
		t.Fatalf("status = %s", created.Status)
	}
	if created.ID == "" || created.CreatedAt == "" || created.UpdatedAt == "" {
		t.Fatalf("generated fields missing: %+v", created)
	}
	if err := first.Close(); err != nil {
		t.Fatalf("close: %v", err)
	}

	second, err := Open(path)
	if err != nil {
		t.Fatalf("reopen: %v", err)
	}
	defer second.Close()

	got, err := second.Get(ctx, created.ID)
	if err != nil {
		t.Fatalf("get after reopen: %v", err)
	}
	if got.Title != "入口墙面破损" || got.Description != "左侧墙体存在裂缝" || got.Priority != model.PriorityHigh {
		t.Fatalf("round trip = %+v", got)
	}
	if got.Position.X != 0.42 || got.Position.Y != 0.03 || got.Position.Z != 1.26 {
		t.Fatalf("position = %+v", got.Position)
	}
}
