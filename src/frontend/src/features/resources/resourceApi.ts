import type {
  ProjectResource,
  ResourceErrorBody,
  ResourceFilters,
  SaveResourceInput,
} from '@/features/resources/types'

export class ResourceApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly body: ResourceErrorBody,
  ) {
    super(body.message)
  }
}

async function request<T>(
  path: string,
  accessToken: string,
  init: RequestInit = {},
) {
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
    const fallback: ResourceErrorBody = {
      code: 'resource_request_failed',
      message: 'Unable to complete the resource request. Please try again.',
    }
    try {
      const body = (await response.json()) as Partial<ResourceErrorBody>
      throw new ResourceApiError(response.status, {
        code: body.code ?? fallback.code,
        message: body.message ?? fallback.message,
        errors: body.errors,
      })
    } catch (error) {
      if (error instanceof ResourceApiError) throw error
      throw new ResourceApiError(response.status, fallback)
    }
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

function resourcePath(workspaceId: string, projectId: string) {
  return `/api/workspaces/${workspaceId}/projects/${projectId}/resources`
}

export const resourceApi = {
  list(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    filters: ResourceFilters,
  ) {
    const query = new URLSearchParams()
    if (filters.search) query.set('search', filters.search)
    if (filters.type) query.set('type', filters.type)
    const suffix = query.size ? `?${query}` : ''
    return request<ProjectResource[]>(
      `${resourcePath(workspaceId, projectId)}${suffix}`,
      accessToken,
    )
  },
  create(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    input: SaveResourceInput,
  ) {
    return request<ProjectResource>(
      resourcePath(workspaceId, projectId),
      accessToken,
      {
        method: 'POST',
        body: JSON.stringify(input),
      },
    )
  },
  update(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    resourceId: string,
    input: SaveResourceInput,
  ) {
    return request<ProjectResource>(
      `${resourcePath(workspaceId, projectId)}/${resourceId}`,
      accessToken,
      { method: 'PUT', body: JSON.stringify(input) },
    )
  },
  delete(
    accessToken: string,
    workspaceId: string,
    projectId: string,
    resourceId: string,
  ) {
    return request<void>(
      `${resourcePath(workspaceId, projectId)}/${resourceId}`,
      accessToken,
      { method: 'DELETE' },
    )
  },
}
