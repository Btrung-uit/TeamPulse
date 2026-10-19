import { useQuery } from '@tanstack/react-query'

import { sharedQueryOptions } from '@/app/providers/sharedQueryOptions'
import { getDashboard } from '@/features/dashboard/dashboardApi'
import { useAuth } from '@/features/auth/useAuth'

export const dashboardKeys = {
  project: (workspaceId: string, projectId: string) =>
    ['dashboard', workspaceId, projectId] as const,
}

export function useDashboard(workspaceId: string, projectId: string) {
  const { accessToken } = useAuth()

  return useQuery({
    queryKey: dashboardKeys.project(workspaceId, projectId),
    queryFn: () => getDashboard(accessToken!, workspaceId, projectId),
    enabled: Boolean(accessToken && workspaceId && projectId),
    ...sharedQueryOptions,
  })
}
