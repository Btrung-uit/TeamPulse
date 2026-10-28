import type { CSSProperties } from 'react'
import {
  Activity,
  ArrowUpRight,
  CircleAlert,
  CircleHelp,
  Clock3,
  GitPullRequest,
  ListTodo,
  RefreshCw,
  Presentation,
  TrendingUp,
  Users,
} from 'lucide-react'
import { Link } from 'react-router-dom'

import { Button } from '@/components/ui/Button/Button'
import { EmptyState } from '@/components/ui/EmptyState/EmptyState'
import { StatusBadge } from '@/components/ui/StatusBadge/StatusBadge'
import { SystemState } from '@/components/ui/SystemState/SystemState'
import { useDashboard } from '@/features/dashboard/dashboardQueries'
import type { DashboardActivity } from '@/features/dashboard/types'
import { projectPath } from '@/features/projects/projectPaths'
import { useProject } from '@/features/projects/useProject'
import {
  dueLabel,
  memberInitials,
  statusForCard,
} from '@/features/tasks/taskBoardModel'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

const statusLabels = {
  Todo: 'Todo',
  InProgress: 'In progress',
  EvidenceLinked: 'Evidence linked',
  InReview: 'In review',
  NeedsChanges: 'Needs changes',
  Blocked: 'Blocked',
  Done: 'Done',
}

function DashboardSkeleton() {
  return (
    <div
      className="dashboard-skeleton"
      role="status"
      aria-live="polite"
      aria-label="Loading dashboard"
    >
      <div className="metric-grid">
        {Array.from({ length: 4 }, (_, index) => (
          <div className="metric-card skeleton-block" key={index} />
        ))}
      </div>
      <div className="dashboard-grid">
        <div className="dashboard-panel skeleton-block" />
        <div className="dashboard-panel skeleton-block" />
      </div>
    </div>
  )
}

function activityText(activity: DashboardActivity) {
  const code = activity.taskCode ? `${activity.taskCode} ` : ''
  const descriptions: Record<string, string> = {
    TaskCreated: `created ${code.trim()}`,
    TaskUpdated: `updated ${code.trim()}`,
    TaskStatusChanged: `moved ${code}to ${activity.newValue ?? 'a new status'}`,
    TaskEvidenceAdded: `linked evidence to ${code.trim()}`,
    TaskReviewAccepted: `accepted ${code.trim()}`,
    TaskChangesRequested: `requested changes on ${code.trim()}`,
    ProjectCreated: 'created this project',
    ProjectUpdated: 'updated this project',
  }
  return descriptions[activity.action] ?? `updated ${code || 'the project'}`
}

function formatDateTime(value: string | null) {
  if (!value) return 'No accepted contribution yet'
  return new Intl.DateTimeFormat('en', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value))
}

function DashboardPage() {
  const { currentWorkspace } = useWorkspace()
  const { currentProject } = useProject()
  const workspaceId = currentWorkspace!.id
  const projectId = currentProject!.id
  const projectBase = projectPath(workspaceId, projectId)
  const dashboardQuery = useDashboard(workspaceId, projectId)
  const dashboard = dashboardQuery.data

  if (dashboardQuery.isLoading) return <DashboardSkeleton />

  if (dashboardQuery.isError || !dashboard) {
    return (
      <SystemState
        className="dashboard-state"
        eyebrow="Connection issue"
        title="We could not load this dashboard."
        description="Your project data is safe. Retry when the API is available."
        icon={<CircleAlert size={30} />}
        primaryAction={
          <Button
            leadingIcon={<RefreshCw size={17} />}
            onClick={() => void dashboardQuery.refetch()}
          >
            Try again
          </Button>
        }
      />
    )
  }

  if (dashboard.kpis.totalTasks === 0) {
    return (
      <div className="dashboard-page">
        <header className="page-header">
          <div>
            <p className="eyebrow">{currentProject!.name}</p>
            <h1>Project dashboard</h1>
          </div>
          <Link
            className="page-header__action"
            to={`${projectBase}/presentation`}
          >
            Present project
            <Presentation aria-hidden="true" size={17} />
          </Link>
        </header>
        <EmptyState
          icon={<ListTodo size={26} />}
          title="No project activity yet"
          description="Create the first task to start tracking progress, workload and accepted contributions."
          primaryAction={
            <Link
              className="ui-button ui-button--primary ui-button--md"
              to={`${projectBase}/tasks`}
            >
              Open task board
            </Link>
          }
        />
      </div>
    )
  }

  const metrics = [
    {
      label: 'Project progress',
      value: `${dashboard.kpis.progressPercent}%`,
      detail: `${dashboard.kpis.doneTasks} of ${dashboard.kpis.totalTasks} tasks done`,
      icon: TrendingUp,
      tone: 'accent',
      to: `${projectBase}/tasks?view=open`,
    },
    {
      label: 'Open tasks',
      value: dashboard.kpis.openTasks,
      detail: 'Across the current project',
      icon: ListTodo,
      tone: 'neutral',
      to: `${projectBase}/tasks?view=open`,
    },
    {
      label: 'Waiting review',
      value: dashboard.kpis.inReviewTasks,
      detail: 'Ready for a reviewer',
      icon: GitPullRequest,
      tone: 'info',
      to: `${projectBase}/tasks?status=InReview`,
    },
    {
      label: 'Blocked / due soon',
      value: `${dashboard.kpis.blockedTasks} / ${dashboard.kpis.dueSoonTasks}`,
      detail: 'Needs attention this week',
      icon: CircleAlert,
      tone: 'danger',
      to: `${projectBase}/tasks?attention=true`,
    },
  ]

  return (
    <div className="dashboard-page">
      <header className="page-header">
        <div>
          <p className="eyebrow">{currentProject!.name}</p>
          <h1>Project dashboard</h1>
          <p>
            Progress, workload and accepted contribution evidence in one view.
          </p>
        </div>
        <div className="page-header__actions">
          <Link
            className="page-header__action"
            to={`${projectBase}/presentation`}
          >
            Present project
            <Presentation aria-hidden="true" size={17} />
          </Link>
          <Link className="page-header__action" to={`${projectBase}/my-tasks`}>
            View my tasks
            <ArrowUpRight aria-hidden="true" size={17} />
          </Link>
        </div>
      </header>

      <section className="metric-grid" aria-label="Project summary">
        {metrics.map((metric) => {
          const Icon = metric.icon
          return (
            <Link
              className="metric-card metric-card--link"
              key={metric.label}
              to={metric.to}
            >
              <div className={`metric-card__icon is-${metric.tone}`}>
                <Icon aria-hidden="true" size={18} />
              </div>
              <p>{metric.label}</p>
              <strong>{metric.value}</strong>
              <span>{metric.detail}</span>
            </Link>
          )
        })}
      </section>

      <div className="dashboard-grid">
        <section className="dashboard-panel dashboard-panel--progress">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Project health</p>
              <h2>Status breakdown</h2>
            </div>
            <span className="panel-heading__meta">
              {dashboard.kpis.totalTasks} active tasks
            </span>
          </div>
          <div className="progress-overview">
            <div
              className="progress-ring"
              aria-label={`${dashboard.kpis.progressPercent} percent complete`}
              style={
                {
                  '--dashboard-progress': `${dashboard.kpis.progressPercent}%`,
                } as CSSProperties
              }
            >
              <div>
                <strong>{dashboard.kpis.progressPercent}%</strong>
                <span>complete</span>
              </div>
            </div>
            <div className="progress-breakdown">
              {dashboard.statusBreakdown.map((item) => (
                <div key={item.status}>
                  <span>
                    <i className={`is-${statusForCard[item.status]}`} />
                    {statusLabels[item.status]}
                  </span>
                  <strong>{item.count}</strong>
                </div>
              ))}
            </div>
          </div>
          {dashboard.kpis.blockedTasks > 0 && (
            <Link
              className="risk-callout"
              to={`${projectBase}/tasks?status=Blocked`}
            >
              <CircleAlert aria-hidden="true" size={18} />
              <div>
                <strong>
                  {dashboard.kpis.blockedTasks} blocked task
                  {dashboard.kpis.blockedTasks === 1 ? '' : 's'} need attention.
                </strong>
                <span>
                  Open the board to inspect ownership and dependencies.
                </span>
              </div>
              <ArrowUpRight aria-hidden="true" size={17} />
            </Link>
          )}
        </section>

        <section className="dashboard-panel dashboard-panel--workload">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Capacity</p>
              <h2>Team workload</h2>
            </div>
            <Users aria-hidden="true" size={20} />
          </div>
          <div className="workload-list">
            {dashboard.workload.map((member) => (
              <article key={member.userId}>
                <span className="activity-avatar" aria-hidden="true">
                  {memberInitials(member.displayName)}
                </span>
                <div>
                  <strong>{member.displayName}</strong>
                  <span>{member.role}</span>
                </div>
                <dl>
                  <div>
                    <dt>Open</dt>
                    <dd>{member.openTasks}</dd>
                  </div>
                  <div>
                    <dt>Blocked</dt>
                    <dd>{member.blockedTasks}</dd>
                  </div>
                  <div>
                    <dt>Due soon</dt>
                    <dd>{member.dueSoonTasks}</dd>
                  </div>
                </dl>
              </article>
            ))}
          </div>
        </section>

        <section className="dashboard-panel dashboard-panel--tasks">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Focus</p>
              <h2>My priority tasks</h2>
            </div>
            <Link to={`${projectBase}/my-tasks`}>View all</Link>
          </div>
          {dashboard.myPriorityTasks.length === 0 ? (
            <p className="dashboard-panel__empty">
              You have no open tasks requiring attention.
            </p>
          ) : (
            <div className="priority-list">
              {dashboard.myPriorityTasks.map((task) => (
                <Link
                  className="priority-task"
                  to={`${projectBase}/tasks?task=${task.id}`}
                  key={task.id}
                >
                  <div>
                    <span className="priority-task__code">
                      {task.code} · {task.priority}
                    </span>
                    <h3>{task.title}</h3>
                  </div>
                  <StatusBadge status={statusForCard[task.status]} />
                  <span className="priority-task__due">
                    <Clock3 aria-hidden="true" size={14} />
                    {dueLabel(task.dueOn)}
                  </span>
                </Link>
              ))}
            </div>
          )}
        </section>

        <section className="dashboard-panel dashboard-panel--activity">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Team pulse</p>
              <h2>Recent activity</h2>
            </div>
            <Activity aria-hidden="true" size={20} />
          </div>
          {dashboard.recentActivity.length === 0 ? (
            <p className="dashboard-panel__empty">
              No recent activity in this project.
            </p>
          ) : (
            <ol className="activity-list">
              {dashboard.recentActivity.map((item) => (
                <li key={item.id}>
                  <span className="activity-avatar" aria-hidden="true">
                    {memberInitials(item.actorDisplayName)}
                  </span>
                  <p>
                    <strong>{item.actorDisplayName}</strong>{' '}
                    {activityText(item)}
                    <time dateTime={item.occurredAtUtc}>
                      {formatDateTime(item.occurredAtUtc)}
                    </time>
                  </p>
                </li>
              ))}
            </ol>
          )}
        </section>

        <section className="dashboard-panel dashboard-panel--contribution">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Contribution summary</p>
              <h2>Accepted work, evidence and reviews</h2>
            </div>
            <span className="panel-heading__meta">
              No score or commit-count ranking
            </span>
          </div>
          <Link
            className="context-help-link"
            to={`/workspaces/${workspaceId}/help#contribution`}
          >
            <CircleHelp aria-hidden="true" size={15} />
            How contribution is counted
          </Link>
          <div className="contribution-table-wrap">
            <table className="contribution-table">
              <thead>
                <tr>
                  <th>Member</th>
                  <th>Accepted tasks</th>
                  <th>Accepted evidence</th>
                  <th>Reviews</th>
                  <th>Latest contribution</th>
                </tr>
              </thead>
              <tbody>
                {dashboard.contributions.map((member) => (
                  <tr key={member.userId}>
                    <th scope="row">
                      <strong>{member.displayName}</strong>
                      <span>{member.role}</span>
                    </th>
                    <td>{member.acceptedTasks}</td>
                    <td>{member.acceptedEvidence}</td>
                    <td>{member.reviews}</td>
                    <td>{formatDateTime(member.lastContributedAtUtc)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  )
}

export default DashboardPage
