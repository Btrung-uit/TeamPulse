import type { TaskPriority, TaskStatus } from '@/features/tasks/types'

export interface DashboardKpis {
  totalTasks: number
  doneTasks: number
  openTasks: number
  inReviewTasks: number
  blockedTasks: number
  dueSoonTasks: number
  progressPercent: number
}

export interface DashboardStatusCount {
  status: TaskStatus
  count: number
}

export interface DashboardWorkload {
  userId: string
  displayName: string
  role: 'Leader' | 'Member'
  openTasks: number
  blockedTasks: number
  dueSoonTasks: number
}

export interface DashboardPriorityTask {
  id: string
  code: string
  title: string
  status: TaskStatus
  priority: TaskPriority
  dueOn: string | null
}

export interface DashboardActivity {
  id: string
  actorUserId: string | null
  actorDisplayName: string
  action: string
  entityId: string
  taskCode: string | null
  previousValue: string | null
  newValue: string | null
  occurredAtUtc: string
}

export interface DashboardContribution {
  userId: string
  displayName: string
  role: 'Leader' | 'Member'
  acceptedTasks: number
  acceptedEvidence: number
  reviews: number
  lastContributedAtUtc: string | null
}

export interface ProjectDashboard {
  kpis: DashboardKpis
  statusBreakdown: DashboardStatusCount[]
  workload: DashboardWorkload[]
  myPriorityTasks: DashboardPriorityTask[]
  recentActivity: DashboardActivity[]
  contributions: DashboardContribution[]
}
