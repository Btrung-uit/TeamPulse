import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { useAuth } from '@/features/auth/useAuth'
import { taskApi } from '@/features/tasks/taskApi'
import type {
  AddTaskEvidenceInput,
  ChangeTaskStatusInput,
  CreateTaskInput,
  TaskDetails,
  TaskFilters,
  ReviewTaskInput,
  UpdateTaskInput,
} from '@/features/tasks/types'

export const taskKeys = {
  project: (workspaceId: string, projectId: string) =>
    ['tasks', workspaceId, projectId] as const,
  list: (workspaceId: string, projectId: string, filters: TaskFilters) =>
    [...taskKeys.project(workspaceId, projectId), 'list', filters] as const,
  detail: (workspaceId: string, projectId: string, taskId: string) =>
    [...taskKeys.project(workspaceId, projectId), 'detail', taskId] as const,
  dependencies: (workspaceId: string, projectId: string) =>
    [...taskKeys.project(workspaceId, projectId), 'dependencies'] as const,
  collaboration: (workspaceId: string, projectId: string, taskId: string) =>
    [
      ...taskKeys.detail(workspaceId, projectId, taskId),
      'collaboration',
    ] as const,
  members: (workspaceId: string) => ['workspace-members', workspaceId] as const,
  sprints: (workspaceId: string, projectId: string) =>
    ['sprints', workspaceId, projectId] as const,
}

export function useTaskDetails(
  workspaceId: string,
  projectId: string,
  taskId?: string,
) {
  const { accessToken } = useAuth()

  return useQuery({
    queryKey: taskKeys.detail(workspaceId, projectId, taskId ?? ''),
    queryFn: () => taskApi.get(accessToken!, workspaceId, projectId, taskId!),
    enabled: Boolean(accessToken && workspaceId && projectId && taskId),
  })
}

export function useTaskCollaboration(
  workspaceId: string,
  projectId: string,
  taskId?: string,
) {
  const { accessToken } = useAuth()

  return useQuery({
    queryKey: taskKeys.collaboration(workspaceId, projectId, taskId ?? ''),
    queryFn: () =>
      taskApi.getCollaboration(accessToken!, workspaceId, projectId, taskId!),
    enabled: Boolean(accessToken && workspaceId && projectId && taskId),
  })
}

export function useTasks(
  workspaceId: string,
  projectId: string,
  filters: TaskFilters,
  enabled = true,
) {
  const { accessToken } = useAuth()

  return useQuery({
    queryKey: taskKeys.list(workspaceId, projectId, filters),
    queryFn: () =>
      taskApi.listAll(accessToken!, workspaceId, projectId, filters),
    enabled: Boolean(enabled && accessToken && workspaceId && projectId),
  })
}

export function useTaskOptions(
  workspaceId: string,
  projectId: string,
  isEnabled = true,
) {
  const { accessToken } = useAuth()
  const enabled = Boolean(isEnabled && accessToken && workspaceId && projectId)
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

function useTaskMutationSuccess(workspaceId: string, projectId: string) {
  const queryClient = useQueryClient()

  return async (task: TaskDetails) => {
    queryClient.setQueryData(
      taskKeys.detail(workspaceId, projectId, task.id),
      task,
    )
    await queryClient.invalidateQueries({
      queryKey: taskKeys.project(workspaceId, projectId),
    })
  }
}

export function useUpdateTask(
  workspaceId: string,
  projectId: string,
  taskId: string,
) {
  const { accessToken } = useAuth()
  const onSuccess = useTaskMutationSuccess(workspaceId, projectId)

  return useMutation({
    mutationFn: (input: UpdateTaskInput) =>
      taskApi.update(accessToken!, workspaceId, projectId, taskId, input),
    onSuccess,
  })
}

export function useChangeTaskStatus(
  workspaceId: string,
  projectId: string,
  taskId: string,
) {
  const { accessToken } = useAuth()
  const onSuccess = useTaskMutationSuccess(workspaceId, projectId)

  return useMutation({
    mutationFn: (input: ChangeTaskStatusInput) =>
      taskApi.changeStatus(accessToken!, workspaceId, projectId, taskId, input),
    onSuccess,
  })
}

export function useArchiveTask(
  workspaceId: string,
  projectId: string,
  taskId: string,
) {
  const { accessToken } = useAuth()
  const onSuccess = useTaskMutationSuccess(workspaceId, projectId)

  return useMutation({
    mutationFn: (rowVersion: string) =>
      taskApi.archive(accessToken!, workspaceId, projectId, taskId, rowVersion),
    onSuccess,
  })
}

function useCollaborationMutationSuccess(
  workspaceId: string,
  projectId: string,
  taskId: string,
) {
  const queryClient = useQueryClient()

  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: taskKeys.collaboration(workspaceId, projectId, taskId),
      }),
      queryClient.invalidateQueries({
        queryKey: taskKeys.detail(workspaceId, projectId, taskId),
      }),
      queryClient.invalidateQueries({
        queryKey: taskKeys.project(workspaceId, projectId),
      }),
    ])
  }
}

export function useAddTaskEvidence(
  workspaceId: string,
  projectId: string,
  taskId: string,
) {
  const { accessToken } = useAuth()
  const onSuccess = useCollaborationMutationSuccess(
    workspaceId,
    projectId,
    taskId,
  )

  return useMutation({
    mutationFn: (input: AddTaskEvidenceInput) =>
      taskApi.addEvidence(accessToken!, workspaceId, projectId, taskId, input),
    onSuccess,
  })
}

export function useReviewTask(
  workspaceId: string,
  projectId: string,
  taskId: string,
) {
  const { accessToken } = useAuth()
  const onSuccess = useCollaborationMutationSuccess(
    workspaceId,
    projectId,
    taskId,
  )

  return useMutation({
    mutationFn: (input: ReviewTaskInput) =>
      taskApi.review(accessToken!, workspaceId, projectId, taskId, input),
    onSuccess,
  })
}

export function useTaskDependencies(
  workspaceId: string,
  projectId: string,
  enabled = true,
) {
  const { accessToken } = useAuth()

  return useQuery({
    queryKey: taskKeys.dependencies(workspaceId, projectId),
    queryFn: () =>
      taskApi.listDependencies(accessToken!, workspaceId, projectId),
    enabled: Boolean(enabled && accessToken && workspaceId && projectId),
  })
}

export function useCreateTaskDependency(
  workspaceId: string,
  projectId: string,
) {
  const { accessToken } = useAuth()
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      predecessorTaskId,
      successorTaskId,
    }: {
      predecessorTaskId: string
      successorTaskId: string
    }) =>
      taskApi.createDependency(
        accessToken!,
        workspaceId,
        projectId,
        predecessorTaskId,
        successorTaskId,
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: taskKeys.dependencies(workspaceId, projectId),
      })
    },
  })
}

export function useDeleteTaskDependency(
  workspaceId: string,
  projectId: string,
) {
  const { accessToken } = useAuth()
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      predecessorTaskId,
      successorTaskId,
    }: {
      predecessorTaskId: string
      successorTaskId: string
    }) =>
      taskApi.deleteDependency(
        accessToken!,
        workspaceId,
        projectId,
        predecessorTaskId,
        successorTaskId,
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: taskKeys.dependencies(workspaceId, projectId),
      })
    },
  })
}
