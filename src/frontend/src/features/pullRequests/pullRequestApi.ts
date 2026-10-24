import type {
  PullRequestFeed,
  PullRequestFilter,
} from '@/features/pullRequests/types'

export async function listPullRequests(
  accessToken: string,
  workspaceId: string,
  projectId: string,
  filter: PullRequestFilter,
  signal?: AbortSignal,
) {
  const parameters = new URLSearchParams({ filter })
  const response = await fetch(
    `/api/workspaces/${workspaceId}/projects/${projectId}/integrations/github/pull-requests?${parameters.toString()}`,
    {
      signal,
      credentials: 'include',
      headers: {
        Accept: 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
    },
  )

  if (!response.ok) {
    throw new Error('Pull requests are unavailable.')
  }

  return (await response.json()) as PullRequestFeed
}
