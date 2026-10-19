import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { sharedQueryOptions } from '@/app/providers/sharedQueryOptions'
import { useAuth } from '@/features/auth/useAuth'
import { resourceApi } from '@/features/resources/resourceApi'
import type {
  ResourceFilters,
  SaveResourceInput,
} from '@/features/resources/types'

export const resourceKeys = {
  project: (workspaceId: string, projectId: string) =>
    ['resources', workspaceId, projectId] as const,
  list: (workspaceId: string, projectId: string, filters: ResourceFilters) =>
    [...resourceKeys.project(workspaceId, projectId), 'list', filters] as const,
}

export function useResources(
  workspaceId: string,
  projectId: string,
  filters: ResourceFilters,
) {
  const { accessToken } = useAuth()
  return useQuery({
    queryKey: resourceKeys.list(workspaceId, projectId, filters),
    queryFn: () =>
      resourceApi.list(accessToken!, workspaceId, projectId, filters),
    enabled: Boolean(accessToken && workspaceId && projectId),
    ...sharedQueryOptions,
  })
}

function useInvalidateResources(workspaceId: string, projectId: string) {
  const queryClient = useQueryClient()
  return () =>
    queryClient.invalidateQueries({
      queryKey: resourceKeys.project(workspaceId, projectId),
    })
}

export function useCreateResource(workspaceId: string, projectId: string) {
  const { accessToken } = useAuth()
  const invalidate = useInvalidateResources(workspaceId, projectId)
  return useMutation({
    mutationFn: (input: SaveResourceInput) =>
      resourceApi.create(accessToken!, workspaceId, projectId, input),
    onSuccess: invalidate,
  })
}

export function useUpdateResource(
  workspaceId: string,
  projectId: string,
  resourceId: string,
) {
  const { accessToken } = useAuth()
  const invalidate = useInvalidateResources(workspaceId, projectId)
  return useMutation({
    mutationFn: (input: SaveResourceInput) =>
      resourceApi.update(
        accessToken!,
        workspaceId,
        projectId,
        resourceId,
        input,
      ),
    onSuccess: invalidate,
  })
}

export function useDeleteResource(
  workspaceId: string,
  projectId: string,
  resourceId: string,
) {
  const { accessToken } = useAuth()
  const invalidate = useInvalidateResources(workspaceId, projectId)
  return useMutation({
    mutationFn: () =>
      resourceApi.delete(accessToken!, workspaceId, projectId, resourceId),
    onSuccess: invalidate,
  })
}
