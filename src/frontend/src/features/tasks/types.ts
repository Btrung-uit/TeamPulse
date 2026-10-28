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

export interface TaskDetails extends TaskSummary {
  workspaceId: string
  projectId: string
  description: string | null
  statusBeforeBlocked: TaskStatus | null
  archivedAtUtc: string | null
  createdAtUtc: string
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
  status?: TaskStatus
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

export interface UpdateTaskInput {
  code: string
  title: string
  description?: string
  assigneeUserId?: string
  reviewerUserId?: string
  sprintId?: string
  priority: TaskPriority
  dueOn?: string
  estimateHours?: number
  rowVersion: string
}

export interface ChangeTaskStatusInput {
  status: TaskStatus
  rowVersion: string
}

export interface BulkAssignTaskSprintInput {
  sprintId: string | null
  tasks: Array<{ taskId: string; rowVersion: string }>
}

export type TaskEvidenceType =
  'Commit' | 'PullRequest' | 'Issue' | 'Document' | 'Design' | 'Demo'

export type TaskEvidenceSource = 'Manual' | 'GitHubAutomation'

export interface TaskEvidence {
  id: string
  type: TaskEvidenceType
  url: string
  note: string | null
  source?: TaskEvidenceSource
  submittedBy: TaskMember | null
  createdAtUtc: string
}

export type TaskCommentKind = 'ReviewAccepted' | 'ChangesRequested'

export interface TaskComment {
  id: string
  kind: TaskCommentKind
  body: string
  author: TaskMember
  createdAtUtc: string
}

export interface TaskCollaboration {
  evidence: TaskEvidence[]
  comments: TaskComment[]
}

export interface AddTaskEvidenceInput {
  type: TaskEvidenceType
  url: string
  note?: string
}

export interface ReviewTaskInput {
  decision: 'Accept' | 'RequestChanges'
  comment: string
  rowVersion: string
}

export interface TaskReviewResult {
  taskId: string
  status: TaskStatus
  rowVersion: string
  comment: TaskComment
}

export interface DependencyTask {
  id: string
  code: string
  title: string
  status: TaskStatus
  dueOn: string | null
  isArchived: boolean
}

export interface DependencyRisk {
  code: string
  message: string
}

export interface TaskDependency {
  predecessor: DependencyTask
  successor: DependencyTask
  isAtRisk: boolean
  risks: DependencyRisk[]
  createdAtUtc: string
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

export interface ImportColumn {
  number: number
  name: string
}

export interface ImportSheet {
  name: string
  dataRowCount: number
  columns: ImportColumn[]
}

export interface TaskImportUpload {
  batchId: string
  fileName: string
  fileSizeBytes: number
  sheets: ImportSheet[]
}

export interface TaskImportMapping {
  taskCode: number
  title: number
  assigneeEmail: number | null
  status: number
  priority: number
  dueDate: number | null
  estimateHours: number | null
  dependsOn: number | null
}

export interface TaskImportCellError {
  rowNumber: number
  column: string
  reason: string
  rawValue: string | null
}

export interface TaskImportPreviewRow {
  rowNumber: number
  taskCode: string | null
  title: string | null
  assigneeEmail: string | null
  status: string | null
  priority: string | null
  dueDate: string | null
  estimateHours: string | null
  dependsOn: string | null
  errors: TaskImportCellError[]
}

export interface TaskImportPreview {
  batchId: string
  status: 'Validated' | 'ValidationFailed'
  totalRows: number
  validRows: number
  errorRows: number
  rows: TaskImportPreviewRow[]
}

export interface TaskImportConfirmation {
  batchId: string
  status: 'Confirmed'
  importedTaskCount: number
  confirmedAtUtc: string
  alreadyConfirmed: boolean
}

export interface TaskImportHistoryItem {
  batchId: string
  fileName: string
  status: 'Uploaded' | 'Validated' | 'ValidationFailed' | 'Confirmed'
  totalRows: number
  validRows: number
  errorRows: number
  importedTaskCount: number
  uploadedAtUtc: string
  confirmedAtUtc: string | null
}
