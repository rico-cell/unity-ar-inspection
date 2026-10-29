// Package store persists issues in a SQLite file so they survive process restarts.
package store

import (
	"context"
	"crypto/rand"
	"database/sql"
	"encoding/hex"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"time"

	"inspection/server/internal/model"

	_ "modernc.org/sqlite"
)

// ErrNotFound is returned when an issue id does not exist.
var ErrNotFound = errors.New("issue not found")

// Store is a single-connection SQLite database.
type Store struct {
	db *sql.DB
}

// Open creates the parent directory, opens (or creates) the database, and migrates.
func Open(path string) (*Store, error) {
	if path == "" {
		return nil, fmt.Errorf("database path is empty")
	}
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return nil, fmt.Errorf("create database directory: %w", err)
	}
	db, err := sql.Open("sqlite", path)
	if err != nil {
		return nil, fmt.Errorf("open sqlite: %w", err)
	}
	// One connection keeps the pure-Go SQLite driver simple under concurrent HTTP requests.
	db.SetMaxOpenConns(1)
	s := &Store{db: db}
	if err := s.migrate(); err != nil {
		_ = db.Close()
		return nil, err
	}
	return s, nil
}

// Close closes the database.
func (s *Store) Close() error {
	if s == nil || s.db == nil {
		return nil
	}
	return s.db.Close()
}

// Ping checks that the database answers.
func (s *Store) Ping(ctx context.Context) error {
	return s.db.PingContext(ctx)
}

func (s *Store) migrate() error {
	pragmas := []string{
		`PRAGMA journal_mode = WAL`,
		`PRAGMA busy_timeout = 5000`,
	}
	for _, stmt := range pragmas {
		if _, err := s.db.Exec(stmt); err != nil {
			return fmt.Errorf("sqlite pragma: %w", err)
		}
	}
	_, err := s.db.Exec(`
		CREATE TABLE IF NOT EXISTS issues (
			id TEXT PRIMARY KEY,
			title TEXT NOT NULL,
			description TEXT NOT NULL,
			priority TEXT NOT NULL,
			status TEXT NOT NULL,
			pos_x REAL NOT NULL,
			pos_y REAL NOT NULL,
			pos_z REAL NOT NULL,
			created_at TEXT NOT NULL,
			updated_at TEXT NOT NULL
		)`)
	if err != nil {
		return fmt.Errorf("create issues table: %w", err)
	}
	if _, err := s.db.Exec(`CREATE INDEX IF NOT EXISTS idx_issues_created ON issues (created_at)`); err != nil {
		return fmt.Errorf("create issues index: %w", err)
	}
	return nil
}

// Create inserts an issue with a new id, status open, and timestamps.
func (s *Store) Create(ctx context.Context, title, description, priority string, pos model.Position) (model.Issue, error) {
	id, err := newID()
	if err != nil {
		return model.Issue{}, err
	}
	now := time.Now().UTC().Format(time.RFC3339Nano)
	issue := model.Issue{
		ID:          id,
		Title:       title,
		Description: description,
		Priority:    priority,
		Status:      model.StatusOpen,
		Position:    pos,
		CreatedAt:   now,
		UpdatedAt:   now,
	}
	_, err = s.db.ExecContext(ctx, `
		INSERT INTO issues (
			id, title, description, priority, status, pos_x, pos_y, pos_z, created_at, updated_at
		) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
		issue.ID, issue.Title, issue.Description, issue.Priority, issue.Status,
		issue.Position.X, issue.Position.Y, issue.Position.Z, issue.CreatedAt, issue.UpdatedAt,
	)
	if err != nil {
		return model.Issue{}, fmt.Errorf("insert issue: %w", err)
	}
	return issue, nil
}

// List returns issues, newest first.
func (s *Store) List(ctx context.Context) ([]model.Issue, error) {
	rows, err := s.db.QueryContext(ctx, `
		SELECT id, title, description, priority, status, pos_x, pos_y, pos_z, created_at, updated_at
		FROM issues
		ORDER BY created_at DESC, id DESC`)
	if err != nil {
		return nil, fmt.Errorf("list issues: %w", err)
	}
	defer rows.Close()
	out := make([]model.Issue, 0)
	for rows.Next() {
		issue, err := scanIssue(rows)
		if err != nil {
			return nil, err
		}
		out = append(out, issue)
	}
	if err := rows.Err(); err != nil {
		return nil, err
	}
	return out, nil
}

// Get loads one issue.
func (s *Store) Get(ctx context.Context, id string) (model.Issue, error) {
	row := s.db.QueryRowContext(ctx, `
		SELECT id, title, description, priority, status, pos_x, pos_y, pos_z, created_at, updated_at
		FROM issues WHERE id = ?`, id)
	issue, err := scanIssue(row)
	if errors.Is(err, sql.ErrNoRows) {
		return model.Issue{}, ErrNotFound
	}
	if err != nil {
		return model.Issue{}, err
	}
	return issue, nil
}

// UpdateStatus sets status and updatedAt. Other fields stay as they are.
func (s *Store) UpdateStatus(ctx context.Context, id, status string) (model.Issue, error) {
	now := time.Now().UTC().Format(time.RFC3339Nano)
	res, err := s.db.ExecContext(ctx, `UPDATE issues SET status = ?, updated_at = ? WHERE id = ?`, status, now, id)
	if err != nil {
		return model.Issue{}, fmt.Errorf("update status: %w", err)
	}
	n, err := res.RowsAffected()
	if err != nil {
		return model.Issue{}, err
	}
	if n == 0 {
		return model.Issue{}, ErrNotFound
	}
	return s.Get(ctx, id)
}

// Delete removes an issue.
func (s *Store) Delete(ctx context.Context, id string) error {
	res, err := s.db.ExecContext(ctx, `DELETE FROM issues WHERE id = ?`, id)
	if err != nil {
		return fmt.Errorf("delete issue: %w", err)
	}
	n, err := res.RowsAffected()
	if err != nil {
		return err
	}
	if n == 0 {
		return ErrNotFound
	}
	return nil
}

type scanner interface {
	Scan(dest ...any) error
}

func scanIssue(sc scanner) (model.Issue, error) {
	var issue model.Issue
	err := sc.Scan(
		&issue.ID, &issue.Title, &issue.Description, &issue.Priority, &issue.Status,
		&issue.Position.X, &issue.Position.Y, &issue.Position.Z, &issue.CreatedAt, &issue.UpdatedAt,
	)
	if err != nil {
		return model.Issue{}, err
	}
	return issue, nil
}

func newID() (string, error) {
	var buf [16]byte
	if _, err := rand.Read(buf[:]); err != nil {
		return "", fmt.Errorf("generate id: %w", err)
	}
	return "iss_" + hex.EncodeToString(buf[:]), nil
}
