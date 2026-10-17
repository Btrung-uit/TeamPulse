import type { TaskPriority as CardPriority } from '@/components/ui/TaskCard/TaskCard'
import type { TaskStatus as CardStatus } from '@/components/ui/StatusBadge/StatusBadge'
import type {
  TaskPriority,
  TaskSort,
  TaskStatus,
  TaskSummary,
} from '@/features/tasks/types'

export const taskColumns: ReadonlyArray<{
  status: TaskStatus
  label: string
}> = [
  { status: 'Todo', label: 'Todo' },
  { status: 'InProgress', label: 'In progress' },
  { status: 'EvidenceLinked', label: 'Evidence linked' },
  { status: 'InReview', label: 'In review' },
  { status: 'NeedsChanges', label: 'Needs changes' },
  { status: 'Blocked', label: 'Blocked' },
  { status: 'Done', label: 'Done' },
]

export const statusForCard: Record<TaskStatus, CardStatus> = {
  Todo: 'todo',
  InProgress: 'in-progress',
  EvidenceLinked: 'evidence-linked',
  InReview: 'in-review',
  NeedsChanges: 'needs-changes',
  Blocked: 'blocked',
  Done: 'done',
}

export const priorityForCard: Record<TaskPriority, CardPriority> = {
  Low: 'low',
  Medium: 'medium',
  High: 'high',
  Urgent: 'urgent',
}

const priorityWeight: Record<TaskPriority, number> = {
  Low: 1,
  Medium: 2,
  High: 3,
  Urgent: 4,
}

const taskPriorities: TaskPriority[] = ['Low', 'Medium', 'High', 'Urgent']
const taskSorts: TaskSort[] = [
  'due-asc',
  'updated-desc',
  'code-asc',
  'priority-desc',
]

export function memberInitials(name: string) {
  return name
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase()
}

export function dueLabel(value: string | null) {
  if (!value) return 'No deadline'
  return new Intl.DateTimeFormat('en', {
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(`${value}T00:00:00Z`))
}

export function isOverdue(task: TaskSummary) {
  if (!task.dueOn || task.status === 'Done') return false
  const today = new Date()
  const todayKey = [
    today.getFullYear(),
    String(today.getMonth() + 1).padStart(2, '0'),
    String(today.getDate()).padStart(2, '0'),
  ].join('-')
  return task.dueOn < todayKey
}

export function sortTasks(tasks: TaskSummary[], sort: TaskSort) {
  return [...tasks].sort((left, right) => {
    if (sort === 'updated-desc') {
      return right.updatedAtUtc.localeCompare(left.updatedAtUtc)
    }
    if (sort === 'code-asc') return left.code.localeCompare(right.code)
    if (sort === 'priority-desc') {
      return priorityWeight[right.priority] - priorityWeight[left.priority]
    }

    if (!left.dueOn && !right.dueOn) return left.code.localeCompare(right.code)
    if (!left.dueOn) return 1
    if (!right.dueOn) return -1
    return (
      left.dueOn.localeCompare(right.dueOn) ||
      left.code.localeCompare(right.code)
    )
  })
}

export function readPriority(value: string | null) {
  return taskPriorities.find((priority) => priority === value)
}

export function readSort(value: string | null): TaskSort {
  return taskSorts.find((sort) => sort === value) ?? 'due-asc'
}
