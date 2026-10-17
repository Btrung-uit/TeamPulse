import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { useAuth } from '@/features/auth/useAuth'
import { taskApi } from '@/features/tasks/taskApi'
import type { CreateTaskInput, TaskFilters } from '@/features/tasks/types'

export const taskKeys = {
  project: (workspaceId: string, projectId: string) =>
    ['tasks', workspaceId, projectId] as const,
  list: (workspaceId: string, projectId: string, filters: TaskFilters) =>
    [...taskKeys.project(workspaceId, projectId), 'list', filters] as const,
  members: (workspaceId: string) => ['workspace-members', workspaceId] as const,
  sprints: (workspaceId: string, projectId: string) =>
    ['sprints', workspaceId, projectId] as const,
}

export function useTasks(
  workspaceId: string,
  projectId: string,
  filters: TaskFilters,
) {
  const { accessToken } = useAuth()

  return useQuery({
    queryKey: taskKeys.list(workspaceId, projectId, filters),
    queryFn: () =>
      taskApi.listAll(accessToken!, workspaceId, projectId, filters),
    enabled: Boolean(accessToken && workspaceId && projectId),
  })
}

export function useTaskOptions(workspaceId: string, projectId: string) {
  const { accessToken } = useAuth()
  const enabled = Boolean(accessToken && workspaceId && projectId)
  const members = useQuery({
    queryKey: taskKeys.members(workspaceId),
    queryFn: () => taskApi.listMembers(accessToken!, workspaceId),
    enabled,
  })
  const sprints = useQuery({
    queryKey: taskKeys.sprints(workspaceId, projectId),
    queryFn: () => taskApi.listSprints(accessToken!, workspaceId, projectId),
    enabled,
  })

  return { members, sprints }
}

export function useCreateTask(workspaceId: string, projectId: string) {
  const { accessToken } = useAuth()
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (input: CreateTaskInput) =>
      taskApi.create(accessToken!, workspaceId, projectId, input),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: taskKeys.project(workspaceId, projectId),
      })
    },
  })
}
