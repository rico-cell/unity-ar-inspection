import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { deleteIssue, initialBase, listIssues, normalizeBase, API_BASE_STORAGE_KEY, updateStatus } from './api.ts'
import type { Issue, Priority, Status } from './types.ts'

const STATUSES: { value: Status; label: string }[] = [
  { value: 'open', label: '待处理' },
  { value: 'in_progress', label: '处理中' },
  { value: 'resolved', label: '已解决' },
]

const PRIORITIES: { value: Priority; label: string }[] = [
  { value: 'low', label: '低' },
  { value: 'medium', label: '中' },
  { value: 'high', label: '高' },
]

function labelOf(options: { value: string; label: string }[], value: string): string {
  return options.find((item) => item.value === value)?.label ?? value
}

function formatTime(iso: string): string {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return iso
  return date.toLocaleString()
}

function formatPosition(issue: Issue): string {
  const { x, y, z } = issue.position
  return `x ${x.toFixed(2)}    y ${y.toFixed(2)}    z ${z.toFixed(2)}`
}

export default function App() {
  const [draft, setDraft] = useState(initialBase)
  const [base, setBase] = useState(initialBase)
  const [issues, setIssues] = useState<Issue[] | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [statusFilter, setStatusFilter] = useState<'all' | Status>('all')
  const [priorityFilter, setPriorityFilter] = useState<'all' | Priority>('all')
  const [busyIds, setBusyIds] = useState<string[]>([])
  const [cardErrors, setCardErrors] = useState<Record<string, string>>({})
  const requestId = useRef(0)

  const load = useCallback(async (target: string) => {
    const id = ++requestId.current
    setLoading(true)
    setError(null)
    try {
      const next = await listIssues(target)
      if (id !== requestId.current) return
      setIssues(next)
    } catch (err) {
      if (id !== requestId.current) return
      setError(err instanceof Error ? err.message : '加载失败')
    } finally {
      if (id === requestId.current) setLoading(false)
    }
  }, [])

  useEffect(() => {
    const id = ++requestId.current
    let active = true
    listIssues(base).then(
      (next) => {
        if (!active || id !== requestId.current) return
        setIssues(next)
        setError(null)
        setLoading(false)
      },
      (err: unknown) => {
        if (!active || id !== requestId.current) return
        setError(err instanceof Error ? err.message : '加载失败')
        setLoading(false)
      },
    )
    return () => {
      active = false
    }
  }, [base])

  const visible = useMemo(() => {
    if (!issues) return []
    return issues.filter((issue) => {
      if (statusFilter !== 'all' && issue.status !== statusFilter) return false
      if (priorityFilter !== 'all' && issue.priority !== priorityFilter) return false
      return true
    })
  }, [issues, priorityFilter, statusFilter])

  function connect() {
    const next = normalizeBase(draft)
    if (!next) {
      setError('请填写后端地址。')
      return
    }
    window.localStorage.setItem(API_BASE_STORAGE_KEY, next)
    setDraft(next)
    setLoading(true)
    setError(null)
    if (next !== base) setIssues(null)
    if (next === base) {
      void load(next)
      return
    }
    setBase(next)
  }

  async function changeStatus(issue: Issue, status: Status) {
    if (issue.status === status || busyIds.includes(issue.id)) return
    setBusyIds((ids) => [...ids, issue.id])
    setCardErrors((prev) => {
      const next = { ...prev }
      delete next[issue.id]
      return next
    })
    try {
      const updated = await updateStatus(base, issue.id, status)
      setIssues((current) => current?.map((item) => (item.id === updated.id ? updated : item)) ?? current)
    } catch (err) {
      const message = err instanceof Error ? err.message : '更新失败'
      setCardErrors((prev) => ({ ...prev, [issue.id]: message }))
    } finally {
      setBusyIds((ids) => ids.filter((id) => id !== issue.id))
    }
  }

  async function remove(issue: Issue) {
    if (busyIds.includes(issue.id)) return
    if (!window.confirm(`删除「${issue.title}」？此操作会从数据库里去掉这条问题。`)) return
    setBusyIds((ids) => [...ids, issue.id])
    setCardErrors((prev) => {
      const next = { ...prev }
      delete next[issue.id]
      return next
    })
    try {
      await deleteIssue(base, issue.id)
      setIssues((current) => current?.filter((item) => item.id !== issue.id) ?? current)
    } catch (err) {
      const message = err instanceof Error ? err.message : '删除失败'
      setCardErrors((prev) => ({ ...prev, [issue.id]: message }))
    } finally {
      setBusyIds((ids) => ids.filter((id) => id !== issue.id))
    }
  }

  return (
    <div className="page">
      <header className="masthead">
        <p className="eyebrow">现场巡检</p>
        <h1>问题列表</h1>
        <p className="lede">
          Unity 在真实平面上放下的标记会提交到这里。改状态会写回同一个数据库。
        </p>
      </header>

      <form
        className="panel connect"
        onSubmit={(event) => {
          event.preventDefault()
          connect()
        }}
      >
        <label>
          后端地址
          <input
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            spellCheck={false}
            inputMode="url"
            aria-label="后端地址"
          />
        </label>
        <div className="connect-actions">
          <button type="submit">重新连接</button>
          <button type="button" className="ghost" onClick={() => void load(base)} disabled={loading}>
            刷新
          </button>
        </div>
      </form>

      {error ? (
        <section className="banner error" role="alert">
          <h2>无法获取问题列表</h2>
          <p>{error}</p>
          <p className="mono">当前地址 {base}</p>
          <button type="button" onClick={() => void load(base)}>
            重试
          </button>
        </section>
      ) : null}

      {loading && issues === null && !error ? <p className="status-line">正在加载问题…</p> : null}
      {loading && issues !== null ? <p className="status-line">正在刷新…</p> : null}

      {!loading && !error && issues !== null && issues.length === 0 ? (
        <section className="panel empty">
          <h2>还没有巡检问题</h2>
          <p>
            用手机打开 Unity 工程，对准地面或桌面，点已识别的平面放下标记，再提交标题。也可以按 README 里的 curl
            先写一条进数据库。
          </p>
        </section>
      ) : null}

      {issues !== null && issues.length > 0 ? (
        <>
          <div className="filters">
            <label>
              状态
              <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as 'all' | Status)}>
                <option value="all">全部</option>
                {STATUSES.map((item) => (
                  <option key={item.value} value={item.value}>
                    {item.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              优先级
              <select
                value={priorityFilter}
                onChange={(event) => setPriorityFilter(event.target.value as 'all' | Priority)}
              >
                <option value="all">全部</option>
                {PRIORITIES.map((item) => (
                  <option key={item.value} value={item.value}>
                    {item.label}
                  </option>
                ))}
              </select>
            </label>
            <p className="count">
              共 {issues.length} 条，显示 {visible.length} 条
            </p>
          </div>

          {visible.length === 0 ? (
            <section className="panel empty">
              <h2>没有符合筛选的问题</h2>
              <p>换一个状态或优先级，或选「全部」。</p>
            </section>
          ) : (
            <ul className="cards">
              {visible.map((issue) => {
                const busy = busyIds.includes(issue.id)
                return (
                  <li key={issue.id} className={`card pri-${issue.priority}`}>
                    <div className="card-main">
                      <div className="title-row">
                        <h2>{issue.title}</h2>
                        <span className={`pill pri-${issue.priority}`}>
                          {labelOf(PRIORITIES, issue.priority)}
                          <small>{issue.priority}</small>
                        </span>
                        <span className={`pill status-${issue.status}`}>
                          {labelOf(STATUSES, issue.status)}
                          <small>{issue.status}</small>
                        </span>
                      </div>
                      <p className="description">{issue.description || '没有描述'}</p>
                      <p className="meta mono">{formatPosition(issue)}</p>
                      <p className="meta">
                        创建 {formatTime(issue.createdAt)} · 更新 {formatTime(issue.updatedAt)}
                      </p>
                      <p className="meta mono">{issue.id}</p>
                      {cardErrors[issue.id] ? <p className="card-error">{cardErrors[issue.id]}</p> : null}
                    </div>
                    <div className="card-actions">
                      {STATUSES.map((item) => (
                        <button
                          key={item.value}
                          type="button"
                          disabled={busy}
                          aria-pressed={issue.status === item.value}
                          onClick={() => void changeStatus(issue, item.value)}
                        >
                          {item.label}
                        </button>
                      ))}
                      <button type="button" className="danger" disabled={busy} onClick={() => void remove(issue)}>
                        删除
                      </button>
                    </div>
                  </li>
                )
              })}
            </ul>
          )}
        </>
      ) : null}

      <footer>
        <span>GET /health</span>
        <span>GET /api/issues</span>
        <span>POST /api/issues</span>
        <span>PATCH /api/issues/&#123;id&#125;</span>
      </footer>
    </div>
  )
}
