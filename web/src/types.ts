// Field names are the contract shared with the Go API and the Unity client.
export type Priority = 'low' | 'medium' | 'high'
export type Status = 'open' | 'in_progress' | 'resolved'

export interface IssuePosition {
  x: number
  y: number
  z: number
}

export interface Issue {
  id: string
  title: string
  description: string
  priority: Priority
  status: Status
  position: IssuePosition
  createdAt: string
  updatedAt: string
}

export interface IssueListResponse {
  issues: Issue[]
}
