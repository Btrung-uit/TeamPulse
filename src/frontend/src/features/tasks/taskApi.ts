import type {
  AddTaskEvidenceInput,
  BulkAssignTaskSprintInput,
  ChangeTaskStatusInput,
  CreateTaskInput,
  PagedTaskResponse,
  SprintOption,
  TaskDependency,
  TaskDetails,
  TaskCollaboration,
  TaskEvidence,
  TaskErrorBody,
  TaskFilters,
  TaskSort,
  TaskSummary,
  ReviewTaskInput,
  TaskReviewResult,
  UpdateTaskInput,
  WorkspaceMemberOption,
  TaskImportMapping,
  TaskImportConfirmation,
  TaskImportHistoryItem,
  TaskImportPreview,
  TaskImportUpload,
} from '@/features/tasks/types'

export interface TaskSpreadsheetDownload {
  blob: Blob
  fileName: string
}

export interface TaskExportOptions {
  filters: TaskFilters
  view?: 'open'
  attention?: boolean
  sort: TaskSort
}

export class TaskApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly body: TaskErrorBody,
  ) {
    super(body.message)
  }
}

async function request<T>(
  path: string,
  accessToken: string,
  init: RequestInit = {},
): Promise<T> {
  const response = await fetch(path, {
    ...init,
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      Authorization: `Bearer ${accessToken}`,
      ...(init.body ? { 'Content-Type': 'application/json' } : {}),
      ...init.headers,
    },
  })

  if (!response.ok) {
    const fallback: TaskErrorBody = {
      code: 'task_request_failed',
      message: 'Unable to complete the task request. Please try again.',
    }

    try {
      const body = (await response.json()) as Partial<TaskErrorBody>
      throw new TaskApiError(response.status, {
        code: body.code ?? fallback.code,
        message: body.message ?? fallback.message,
        errors: body.errors,
      })
    } catch (error) {
      if (error instanceof TaskApiError) throw error
      throw new TaskApiError(response.status, fallback)
    }
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

async function requestSpreadsheet(
  path: string,
  accessToken: string,
): Promise<TaskSpreadsheetDownload> {
  const response = await fetch(path, {
    credentials: 'include',
    headers: {
      Accept:
        'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      Authorization: `Bearer ${accessToken}`,
    },
  })

  if (!response.ok) {
    const fallback: TaskErrorBody = {
      code: 'spreadsheet_download_failed',
      message: 'Unable to download the spreadsheet. Please try again.',
    }
    try {
      const body = (await response.json()) as Partial<TaskErrorBody>
      throw new TaskApiError(response.status, {
        code: body.code ?? fallback.code,
        message: body.message ?? fallback.message,
        errors: body.errors,
      })
    } catch (error) {
      if (error instanceof TaskApiError) throw error
      throw new TaskApiError(response.status, fallback)
    }
  }

  const disposition = response.headers.get('Content-Disposition') ?? ''
  const encodedName = disposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1]
  const plainName = disposition.match(/filename="?([^";]+)"?/i)?.[1]
  return {
    blob: await response.blob(),
    fileName: encodedName
      ? decodeURIComponent(encodedName)
      : (plainName ?? 'teampulse-tasks.xlsx'),
  }
}

function projectPath(workspaceId: string, projectId: string) {
  return `/api/workspaces/${workspaceId}/projects/${projectId}`
}

function taskListPath(
  workspaceId: string,
  projectId: string,
  page: number,
  filters: TaskFilters,
) {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: '100',
  })

  if (filters.search) query.set('search', filters.search)
  if (filters.status) query.set('status', filters.status)
  if (filters.priority) query.set('priority', filters.priority)
  if (filters.assigneeUserId) {
    query.set('assigneeUserId', filters.assigneeUserId)
  }
  if (filters.sprintId) query.set('sprintId', filters.sprintId)

  return `${projectPath(workspaceId, projectId)}/tasks?${query}`
}

function taskExportPath(
  workspaceId: string,
  projectId: string,
  options: TaskExportOptions,
) {
  const query = new URLSearchParams()
  const { filters } = options
  if (filters.search) query.set('search', filters.search)
  if (filters.status) query.set('status', filters.status)
  if (filters.priority) query.set('priority', filters.priority)
  if (filters.assigneeUserId) {
    query.set('assigneeUserId', filters.assigneeUserId)
  }
  if (filters.sprintId) query.set('sprintId', filters.sprintId)
  if (options.view) query.set('view', options.view)
  if (options.attention) query.set('attention', 'true')
  query.set('sort', options.sort)
  return `${projectPath(workspaceId, projectId)}/tasks/export?${query}`
}

export const taskApi = {
  async uploadImport(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    file: File,
  ) {
    const form = new FormData()
    form.append('file', file)
    const response = await fetch(
      `${projectPath(workspaceId, projectId)}/tasks/imports`,
      {
        method: 'POST',
        credentials: 'include',
        headers: {
          Accept: 'application/json',
          Authorization: `Bearer ${accessToken}`,
        },
        body: form,
      },
    )
    if (!response.ok) {
      const body = (await response
        .json()
        .catch(() => null)) as TaskErrorBody | null
      throw new TaskApiError(response.status, {
        code: body?.code ?? 'import_upload_failed',
        message: body?.message ?? 'Unable to upload the workbook.',
        errors: body?.errors,
      })
    }
    return (await response.json()) as TaskImportUpload
  },

  previewImport(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    batchId: string,
    sheetName: string,
    mapping: TaskImportMapping,
  ) {
    return request<TaskImportPreview>(
      `${projectPath(workspaceId, projectId)}/tasks/imports/${batchId}/preview`,
      accessToken,
      { method: 'PUT', body: JSON.stringify({ sheetName, mapping }) },
    )
  },

  confirmImport(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    batchId: string,
  ) {
    return request<TaskImportConfirmation>(
      `${projectPath(workspaceId, projectId)}/tasks/imports/${batchId}/confirm`,
      accessToken,
      { method: 'POST' },
    )
  },

  listImportHistory(
    accessToken: string,
    workspaceId: string,
    projectId: string,
  ) {
    return request<TaskImportHistoryItem[]>(
      `${projectPath(workspaceId, projectId)}/tasks/imports`,
      accessToken,
    )
  },

  downloadTemplate(
    accessToken: string,
    workspaceId: string,
    projectId: string,
  ) {
    return requestSpreadsheet(
      `${projectPath(workspaceId, projectId)}/tasks/template`,
      accessToken,
    )
  },

  exportSnapshot(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    options: TaskExportOptions,
  ) {
    return requestSpreadsheet(
      taskExportPath(workspaceId, projectId, options),
      accessToken,
    )
  },

  async listAll(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    filters: TaskFilters,
  ) {
    const firstPage = await request<PagedTaskResponse>(
      taskListPath(workspaceId, projectId, 1, filters),
      accessToken,
    )

    if (firstPage.totalPages <= 1) return firstPage.items

    const remainingPages = await Promise.all(
      Array.from({ length: firstPage.totalPages - 1 }, (_, index) =>
        request<PagedTaskResponse>(
          taskListPath(workspaceId, projectId, index + 2, filters),
          accessToken,
        ),
      ),
    )

    return [...firstPage.items, ...remainingPages.flatMap((page) => page.items)]
  },

  create(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    input: CreateTaskInput,
  ) {
    return request<TaskSummary>(
      `${projectPath(workspaceId, projectId)}/tasks`,
      accessToken,
      {
        method: 'POST',
        body: JSON.stringify(input),
      },
    )
  },

  get(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    taskId: string,
  ) {
    return request<TaskDetails>(
      `${projectPath(workspaceId, projectId)}/tasks/${taskId}`,
      accessToken,
    )
  },

  update(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    taskId: string,
    input: UpdateTaskInput,
  ) {
    return request<TaskDetails>(
      `${projectPath(workspaceId, projectId)}/tasks/${taskId}`,
      accessToken,
      { method: 'PUT', body: JSON.stringify(input) },
    )
  },

  changeStatus(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    taskId: string,
    input: ChangeTaskStatusInput,
  ) {
    return request<TaskDetails>(
      `${projectPath(workspaceId, projectId)}/tasks/${taskId}/status`,
      accessToken,
      { method: 'PATCH', body: JSON.stringify(input) },
    )
  },

  archive(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    taskId: string,
    rowVersion: string,
  ) {
    return request<TaskDetails>(
      `${projectPath(workspaceId, projectId)}/tasks/${taskId}/archive`,
      accessToken,
      { method: 'POST', body: JSON.stringify({ rowVersion }) },
    )
  },

  bulkAssignSprint(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    input: BulkAssignTaskSprintInput,
  ) {
    return request<void>(
      `${projectPath(workspaceId, projectId)}/tasks/sprint`,
      accessToken,
      { method: 'PUT', body: JSON.stringify(input) },
    )
  },

  getCollaboration(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    taskId: string,
  ) {
    return request<TaskCollaboration>(
      `${projectPath(workspaceId, projectId)}/tasks/${taskId}/collaboration`,
      accessToken,
    )
  },

  addEvidence(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    taskId: string,
    input: AddTaskEvidenceInput,
  ) {
    return request<TaskEvidence>(
      `${projectPath(workspaceId, projectId)}/tasks/${taskId}/evidence`,
      accessToken,
      { method: 'POST', body: JSON.stringify(input) },
    )
  },

  review(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    taskId: string,
    input: ReviewTaskInput,
  ) {
    return request<TaskReviewResult>(
      `${projectPath(workspaceId, projectId)}/tasks/${taskId}/review`,
      accessToken,
      { method: 'POST', body: JSON.stringify(input) },
    )
  },

  listDependencies(
    accessToken: string,
    workspaceId: string,
    projectId: string,
  ) {
    return request<TaskDependency[]>(
      `${projectPath(workspaceId, projectId)}/dependencies`,
      accessToken,
    )
  },

  createDependency(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    predecessorTaskId: string,
    successorTaskId: string,
  ) {
    return request<TaskDependency>(
      `${projectPath(workspaceId, projectId)}/dependencies`,
      accessToken,
      {
        method: 'POST',
        body: JSON.stringify({ predecessorTaskId, successorTaskId }),
      },
    )
  },

  deleteDependency(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    predecessorTaskId: string,
    successorTaskId: string,
  ) {
    return request<void>(
      `${projectPath(workspaceId, projectId)}/dependencies/${predecessorTaskId}/${successorTaskId}`,
      accessToken,
      { method: 'DELETE' },
    )
  },

  listMembers(accessToken: string, workspaceId: string) {
    return request<WorkspaceMemberOption[]>(
      `/api/workspaces/${workspaceId}/members`,
      accessToken,
    )
  },

  listSprints(accessToken: string, workspaceId: string, projectId: string) {
    return request<SprintOption[]>(
      `${projectPath(workspaceId, projectId)}/sprints`,
      accessToken,
    )
  },
}
