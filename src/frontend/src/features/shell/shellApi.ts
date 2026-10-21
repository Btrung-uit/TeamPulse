export interface ProjectSearchResult {
  id: string
  name: string
  description: string | null
}

export interface TaskSearchResult {
  id: string
  projectId: string
  projectName: string
  code: string
  title: string
  status: string
}

export interface MemberSearchResult {
  userId: string
  displayName: string
  email: string
  role: 'Leader' | 'Member' | 'Advisor'
}

export interface WorkspaceSearchResults {
  projects: ProjectSearchResult[]
  tasks: TaskSearchResult[]
  members: MemberSearchResult[]
}

export type NotificationKind =
  'Assignment' | 'ReviewRequest' | 'ReviewAccepted' | 'ChangesRequested'

export type NotificationFilter = 'all' | 'unread' | 'reviews'

export interface NotificationItem {
  id: string
  kind: NotificationKind
  title: string
  message: string
  projectId: string
  taskId: string | null
  isRead: boolean
  createdAtUtc: string
}

export interface NotificationFeed {
  items: NotificationItem[]
  unreadCount: number
}

class ShellApiError extends Error {}

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
    throw new ShellApiError('Unable to complete the request. Please try again.')
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export const searchApi = {
  search(
    accessToken: string,
    workspaceId: string,
    query: string,
    signal?: AbortSignal,
  ) {
    const parameters = new URLSearchParams({ query })
    return request<WorkspaceSearchResults>(
      `/api/workspaces/${workspaceId}/search?${parameters.toString()}`,
      accessToken,
      { signal },
    )
  },
}

export const notificationApi = {
  list(accessToken: string, workspaceId: string, filter: NotificationFilter) {
    const parameters = new URLSearchParams({ filter })
    return request<NotificationFeed>(
      `/api/workspaces/${workspaceId}/notifications?${parameters.toString()}`,
      accessToken,
    )
  },

  markRead(accessToken: string, workspaceId: string, notificationId: string) {
    return request<void>(
      `/api/workspaces/${workspaceId}/notifications/${notificationId}/read`,
      accessToken,
      { method: 'PATCH' },
    )
  },

  markAllRead(accessToken: string, workspaceId: string) {
    return request<{ markedCount: number }>(
      `/api/workspaces/${workspaceId}/notifications/read-all`,
      accessToken,
      { method: 'POST' },
    )
  },
}
