import type { Issue, IssueListResponse, Status } from './types.ts'

export const API_BASE_STORAGE_KEY = 'inspection.apiBase'

export function normalizeBase(raw: string): string {
  let value = raw.trim()
  if (!value) return ''
  if (!value.startsWith('http://') && !value.startsWith('https://')) {
    value = `http://${value}`
  }
  while (value.endsWith('/')) value = value.slice(0, -1)
  return value
}

export function initialBase(): string {
  const stored = window.localStorage.getItem(API_BASE_STORAGE_KEY) ?? ''
  const fromEnv = import.meta.env.VITE_API_BASE ?? ''
  return normalizeBase(stored || fromEnv || 'http://127.0.0.1:8787')
}

export async function listIssues(base: string): Promise<Issue[]> {
  const response = await request(base, '/api/issues')
  const body = (await response.json()) as Partial<IssueListResponse>
  if (!Array.isArray(body.issues)) {
    throw new Error('响应里没有 issues 数组')
  }
  return body.issues
}

export async function updateStatus(base: string, id: string, status: Status): Promise<Issue> {
  const response = await request(base, `/api/issues/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ status }),
  })
  return (await response.json()) as Issue
}

export async function deleteIssue(base: string, id: string): Promise<void> {
  const response = await request(base, `/api/issues/${encodeURIComponent(id)}`, {
    method: 'DELETE',
  })
  if (response.status !== 204 && !response.ok) {
    throw new Error(await readError(response))
  }
}

async function request(base: string, path: string, init?: RequestInit): Promise<Response> {
  let response: Response
  try {
    response = await fetch(`${base}${path}`, {
      ...init,
      headers: {
        Accept: 'application/json',
        ...init?.headers,
      },
    })
  } catch {
    throw new Error(`无法连接 ${base}。请确认 Go 服务已启动，并且浏览器能访问这个地址。`)
  }
  if (!response.ok) {
    throw new Error(await readError(response))
  }
  return response
}

async function readError(response: Response): Promise<string> {
  const text = await response.text()
  if (!text) return `HTTP ${response.status}`
  try {
    const body = JSON.parse(text) as { error?: string }
    if (body.error) return body.error
  } catch {
    return text
  }
  return text
}
