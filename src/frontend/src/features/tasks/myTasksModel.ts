import type { TaskStatus, TaskSummary } from '@/features/tasks/types'

export type MyTaskDueFilter =
  'all' | 'overdue' | 'today' | 'next-7-days' | 'no-deadline'

export type MyTaskStatusFilter = 'active' | 'all' | TaskStatus

const dueFilters: MyTaskDueFilter[] = [
  'all',
  'overdue',
  'today',
  'next-7-days',
  'no-deadline',
]

const statuses: TaskStatus[] = [
  'Todo',
  'InProgress',
  'EvidenceLinked',
  'InReview',
  'NeedsChanges',
  'Blocked',
  'Done',
]

const priorityWeight = {
  Low: 1,
  Medium: 2,
  High: 3,
  Urgent: 4,
} as const

export function dateKey(date: Date) {
  return [
    date.getFullYear(),
    String(date.getMonth() + 1).padStart(2, '0'),
    String(date.getDate()).padStart(2, '0'),
  ].join('-')
}

function addDaysKey(today: Date, days: number) {
  const result = new Date(today)
  result.setDate(result.getDate() + days)
  return dateKey(result)
}

export function isMyTaskOverdue(task: TaskSummary, today = new Date()) {
  return Boolean(
    task.dueOn && task.status !== 'Done' && task.dueOn < dateKey(today),
  )
}

export function isMyTaskDueSoon(task: TaskSummary, today = new Date()) {
  if (!task.dueOn || task.status === 'Done') return false
  const todayValue = dateKey(today)
  return task.dueOn >= todayValue && task.dueOn <= addDaysKey(today, 7)
}

export function needsAttention(task: TaskSummary, today = new Date()) {
  return (
    task.status === 'Blocked' ||
    task.status === 'NeedsChanges' ||
    isMyTaskOverdue(task, today) ||
    isMyTaskDueSoon(task, today)
  )
}

export function readMyTaskDueFilter(value: string | null): MyTaskDueFilter {
  return dueFilters.find((filter) => filter === value) ?? 'all'
}

export function readMyTaskStatusFilter(
  value: string | null,
): MyTaskStatusFilter {
  if (value === 'all' || value === 'active') return value
  return statuses.find((status) => status === value) ?? 'active'
}

function matchesDueFilter(
  task: TaskSummary,
  filter: MyTaskDueFilter,
  today: Date,
) {
  if (filter === 'all') return true
  if (filter === 'overdue') return isMyTaskOverdue(task, today)
  if (filter === 'no-deadline') return !task.dueOn
  if (!task.dueOn || task.status === 'Done') return false
  if (filter === 'today') return task.dueOn === dateKey(today)
  return task.dueOn >= dateKey(today) && task.dueOn <= addDaysKey(today, 7)
}

function attentionWeight(task: TaskSummary, today: Date) {
  if (task.status === 'Blocked') return 5
  if (isMyTaskOverdue(task, today)) return 4
  if (task.status === 'NeedsChanges') return 3
  if (isMyTaskDueSoon(task, today)) return 2
  return 1
}

export function filterAndSortMyTasks(
  tasks: TaskSummary[],
  options: {
    search: string
    status: MyTaskStatusFilter
    due: MyTaskDueFilter
    today?: Date
  },
) {
  const today = options.today ?? new Date()
  const search = options.search.trim().toLocaleLowerCase()

  return tasks
    .filter((task) => {
      const statusMatches =
        options.status === 'all' ||
        (options.status === 'active'
          ? task.status !== 'Done'
          : task.status === options.status)
      const searchMatches =
        !search ||
        task.code.toLocaleLowerCase().includes(search) ||
        task.title.toLocaleLowerCase().includes(search)
      return (
        statusMatches &&
        searchMatches &&
        matchesDueFilter(task, options.due, today)
      )
    })
    .sort((left, right) => {
      const attentionDifference =
        attentionWeight(right, today) - attentionWeight(left, today)
      if (attentionDifference) return attentionDifference
      const priorityDifference =
        priorityWeight[right.priority] - priorityWeight[left.priority]
      if (priorityDifference) return priorityDifference
      if (!left.dueOn && !right.dueOn)
        return left.code.localeCompare(right.code)
      if (!left.dueOn) return 1
      if (!right.dueOn) return -1
      return left.dueOn.localeCompare(right.dueOn)
    })
}

export function summarizeMyTasks(tasks: TaskSummary[], today = new Date()) {
  const active = tasks.filter((task) => task.status !== 'Done')
  return {
    active: active.length,
    blocked: active.filter((task) => task.status === 'Blocked').length,
    overdue: active.filter((task) => isMyTaskOverdue(task, today)).length,
    dueSoon: active.filter((task) => isMyTaskDueSoon(task, today)).length,
  }
}
