import type {
  ProjectErrorBody,
  ProjectSummary,
  SaveProjectInput,
} from '@/features/projects/types'

export class ProjectApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly body: ProjectErrorBody,
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
    const fallback: ProjectErrorBody = {
      code: 'project_request_failed',
      message: 'Unable to complete the project request. Please try again.',
    }

    try {
      const body = (await response.json()) as Partial<ProjectErrorBody>
      throw new ProjectApiError(response.status, {
        code: body.code ?? fallback.code,
        message: body.message ?? fallback.message,
        errors: body.errors,
      })
    } catch (error) {
      if (error instanceof ProjectApiError) throw error
      throw new ProjectApiError(response.status, fallback)
    }
  }

  return (await response.json()) as T
}

function projectsPath(workspaceId: string) {
  return `/api/workspaces/${workspaceId}/projects`
}

export const projectApi = {
  list(accessToken: string, workspaceId: string) {
    return request<ProjectSummary[]>(
      `${projectsPath(workspaceId)}?includeArchived=true`,
      accessToken,
    )
  },

  create(accessToken: string, workspaceId: string, input: SaveProjectInput) {
    return request<ProjectSummary>(projectsPath(workspaceId), accessToken, {
      method: 'POST',
      body: JSON.stringify(input),
    })
  },

  update(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    input: SaveProjectInput,
  ) {
    return request<ProjectSummary>(
      `${projectsPath(workspaceId)}/${projectId}`,
      accessToken,
      { method: 'PUT', body: JSON.stringify(input) },
    )
  },

  archive(accessToken: string, workspaceId: string, projectId: string) {
    return request<ProjectSummary>(
      `${projectsPath(workspaceId)}/${projectId}/archive`,
      accessToken,
      { method: 'POST' },
    )
  },
}
