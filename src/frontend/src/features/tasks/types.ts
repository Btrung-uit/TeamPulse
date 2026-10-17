export type TaskStatus =
  | 'Todo'
  | 'InProgress'
  | 'EvidenceLinked'
  | 'InReview'
  | 'NeedsChanges'
  | 'Blocked'
  | 'Done'

export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Urgent'

export interface TaskMember {
  userId: string
  displayName: string
  email: string
}

export interface TaskSprint {
  id: string
  name: string
}

export interface TaskSummary {
  id: string
  code: string
  title: string
  status: TaskStatus
  priority: TaskPriority
  dueOn: string | null
  estimateHours: number | null
  assignee: TaskMember | null
  reviewer: TaskMember | null
  sprint: TaskSprint | null
  isArchived: boolean
  updatedAtUtc: string
  rowVersion: string
}

export interface PagedTaskResponse {
  items: TaskSummary[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface TaskFilters {
  search?: string
  priority?: TaskPriority
  assigneeUserId?: string
  sprintId?: string
}

export type TaskSort = 'due-asc' | 'updated-desc' | 'code-asc' | 'priority-desc'

export interface CreateTaskInput {
  code: string
  title: string
  description?: string
  assigneeUserId?: string
  reviewerUserId?: string
  sprintId?: string
  priority: TaskPriority
  dueOn?: string
  estimateHours?: number
  rowVersion: null
}

export interface WorkspaceMemberOption {
  userId: string
  email: string
  displayName: string
  role: 'Leader' | 'Member' | 'Advisor'
}

export interface SprintOption {
  id: string
  workspaceId: string
  projectId: string
  name: string
  goal: string | null
  startsOn: string
  endsOn: string
  taskCount: number
  createdAtUtc: string
  updatedAtUtc: string
}

export interface TaskErrorBody {
  code: string
  message: string
  errors?: Record<string, string[]>
}
