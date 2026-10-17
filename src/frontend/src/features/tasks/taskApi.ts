import type {
  CreateTaskInput,
  PagedTaskResponse,
  SprintOption,
  TaskErrorBody,
  TaskFilters,
  TaskSummary,
  WorkspaceMemberOption,
} from '@/features/tasks/types'

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

  return (await response.json()) as T
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
  if (filters.priority) query.set('priority', filters.priority)
  if (filters.assigneeUserId) {
    query.set('assigneeUserId', filters.assigneeUserId)
  }
  if (filters.sprintId) query.set('sprintId', filters.sprintId)

  return `${projectPath(workspaceId, projectId)}/tasks?${query}`
}

export const taskApi = {
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
