import type { TaskDetails, TaskStatus } from '@/features/tasks/types'
import type { WorkspaceSummary } from '@/features/workspaces/types'

export const taskStatusLabels: Record<TaskStatus, string> = {
  Todo: 'Todo',
  InProgress: 'In progress',
  EvidenceLinked: 'Evidence linked',
  InReview: 'In review',
  NeedsChanges: 'Needs changes',
  Blocked: 'Blocked',
  Done: 'Done',
}

const transitions: Record<Exclude<TaskStatus, 'Blocked'>, TaskStatus[]> = {
  Todo: ['InProgress', 'Blocked'],
  InProgress: ['Todo', 'EvidenceLinked', 'Blocked'],
  EvidenceLinked: ['InProgress', 'InReview', 'Blocked'],
  InReview: ['InProgress', 'NeedsChanges', 'Done', 'Blocked'],
  NeedsChanges: ['InProgress', 'InReview', 'Blocked'],
  Done: [],
}

export function allowedStatusTransitions(
  task: TaskDetails,
  role: WorkspaceSummary['role'],
  userId: string,
) {
  if (task.isArchived || role === 'Advisor') return []

  const domainTargets =
    task.status === 'Blocked'
      ? task.statusBeforeBlocked
        ? [task.statusBeforeBlocked]
        : []
      : transitions[task.status]

  if (role === 'Leader') return domainTargets

  return domainTargets.filter((target) => {
    if (target === 'Done') return false
    if (target === 'NeedsChanges') return task.reviewer?.userId === userId
    return task.assignee?.userId === userId
  })
}
