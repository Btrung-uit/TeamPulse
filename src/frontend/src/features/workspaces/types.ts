export interface WorkspaceSummary {
  id: string
  name: string
  role: 'Leader' | 'Member' | 'Advisor'
  memberCount: number
}

export type WorkspaceRole = WorkspaceSummary['role']

export interface WorkspaceMember {
  userId: string
  email: string
  displayName: string
  role: WorkspaceRole
}

export interface AddWorkspaceMemberInput {
  email: string
  role: WorkspaceRole
}

export interface WorkspaceErrorBody {
  code: string
  message: string
  errors?: Record<string, string[]>
}
