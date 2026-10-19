import type {
  AddWorkspaceMemberInput,
  WorkspaceErrorBody,
  WorkspaceMember,
  WorkspaceSummary,
} from '@/features/workspaces/types'

export class WorkspaceApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly body: WorkspaceErrorBody,
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
    const fallback: WorkspaceErrorBody = {
      code: 'workspace_request_failed',
      message: 'Unable to load your workspace. Please try again.',
    }

    try {
      const body = (await response.json()) as Partial<WorkspaceErrorBody>
      throw new WorkspaceApiError(response.status, {
        code: body.code ?? fallback.code,
        message: body.message ?? fallback.message,
        errors: body.errors,
      })
    } catch (error) {
      if (error instanceof WorkspaceApiError) {
        throw error
      }

      throw new WorkspaceApiError(response.status, fallback)
    }
  }

  return (await response.json()) as T
}

export const workspaceApi = {
  list(accessToken: string) {
    return request<WorkspaceSummary[]>('/api/workspaces', accessToken)
  },

  create(accessToken: string, name: string) {
    return request<WorkspaceSummary>('/api/workspaces', accessToken, {
      method: 'POST',
      body: JSON.stringify({ name }),
    })
  },

  listMembers(accessToken: string, workspaceId: string) {
    return request<WorkspaceMember[]>(
      `/api/workspaces/${workspaceId}/members`,
      accessToken,
    )
  },

  addMember(
    accessToken: string,
    workspaceId: string,
    input: AddWorkspaceMemberInput,
  ) {
    return request<WorkspaceMember>(
      `/api/workspaces/${workspaceId}/members`,
      accessToken,
      {
        method: 'POST',
        body: JSON.stringify(input),
      },
    )
  },
}
