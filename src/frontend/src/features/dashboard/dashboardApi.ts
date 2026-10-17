import type { ProjectDashboard } from '@/features/dashboard/types'

export async function getDashboard(
  accessToken: string,
  workspaceId: string,
  projectId: string,
): Promise<ProjectDashboard> {
  const response = await fetch(
    `/api/workspaces/${workspaceId}/projects/${projectId}/dashboard`,
    {
      credentials: 'include',
      headers: {
        Accept: 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
    },
  )

  if (!response.ok) {
    throw new Error('Unable to load the project dashboard.')
  }

  return (await response.json()) as ProjectDashboard
}
