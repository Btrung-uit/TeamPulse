export interface WorkspaceSummary {
  id: string
  name: string
  role: 'Leader' | 'Member' | 'Advisor'
  memberCount: number
}

export interface WorkspaceErrorBody {
  code: string
  message: string
  errors?: Record<string, string[]>
}
