export interface ProjectSummary {
  id: string
  workspaceId: string
  name: string
  description: string | null
  isArchived: boolean
  createdAtUtc: string
  updatedAtUtc: string
  archivedAtUtc: string | null
}

export interface SaveProjectInput {
  name: string
  description?: string
}

export interface ProjectErrorBody {
  code: string
  message: string
  errors?: Record<string, string[]>
}
