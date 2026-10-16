import {
  ArrowUpRight,
  CalendarDays,
  CircleAlert,
  Clock3,
  GitPullRequest,
  ListTodo,
  TrendingUp,
} from 'lucide-react'
import { Link } from 'react-router-dom'

const metrics = [
  {
    label: 'Project progress',
    value: '68%',
    detail: '+12% this sprint',
    icon: TrendingUp,
    tone: 'accent',
  },
  {
    label: 'Open tasks',
    value: '14',
    detail: '6 assigned to you',
    icon: ListTodo,
    tone: 'neutral',
  },
  {
    label: 'Waiting review',
    value: '3',
    detail: '1 needs your action',
    icon: GitPullRequest,
    tone: 'info',
  },
  {
    label: 'At risk',
    value: '2',
    detail: 'Due within 48 hours',
    icon: CircleAlert,
    tone: 'danger',
  },
]

const priorityTasks = [
  {
    code: 'TP-034',
    title: 'Complete event registration flow',
    status: 'In progress',
    tone: 'accent',
    due: 'Today',
  },
  {
    code: 'TP-041',
    title: 'Review database migration proposal',
    status: 'In review',
    tone: 'info',
    due: 'Tomorrow',
  },
  {
    code: 'TP-045',
    title: 'Attach evidence to sprint report',
    status: 'Evidence linked',
    tone: 'purple',
    due: 'Oct 19',
  },
]

const recentActivity = [
  {
    initials: 'MA',
    actor: 'Minh Anh',
    action: 'moved TP-031 to In Review',
    time: '12 min ago',
  },
  {
    initials: 'BT',
    actor: 'Bảo Trung',
    action: 'linked PR #28 as evidence',
    time: '36 min ago',
  },
  {
    initials: 'HN',
    actor: 'Hoàng Nam',
    action: 'commented on the database plan',
    time: '1 hr ago',
  },
]

function DashboardPage() {
  return (
    <div className="dashboard-page">
      <header className="page-header">
        <div>
          <p className="eyebrow">CampusEvent · Sprint 03</p>
          <h1>Good morning, Trung.</h1>
          <p>Here is what needs attention across your project today.</p>
        </div>
        <Link className="page-header__action" to="/my-tasks">
          View my tasks
          <ArrowUpRight aria-hidden="true" size={17} />
        </Link>
      </header>

      <section className="metric-grid" aria-label="Project summary">
        {metrics.map((metric) => {
          const Icon = metric.icon

          return (
            <article className="metric-card" key={metric.label}>
              <div className={`metric-card__icon is-${metric.tone}`}>
                <Icon aria-hidden="true" size={18} />
              </div>
              <p>{metric.label}</p>
              <strong>{metric.value}</strong>
              <span>{metric.detail}</span>
            </article>
          )
        })}
      </section>

      <div className="dashboard-grid">
        <section className="dashboard-panel dashboard-panel--progress">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Sprint health</p>
              <h2>Steady progress, two risks</h2>
            </div>
            <span className="panel-heading__meta">8 days left</span>
          </div>

          <div className="progress-overview">
            <div className="progress-ring" aria-label="68 percent complete">
              <div>
                <strong>68%</strong>
                <span>complete</span>
              </div>
            </div>
            <div className="progress-breakdown">
              <div>
                <span>
                  <i className="is-done" /> Done
                </span>
                <strong>17</strong>
              </div>
              <div>
                <span>
                  <i className="is-progress" /> In progress
                </span>
                <strong>8</strong>
              </div>
              <div>
                <span>
                  <i className="is-review" /> In review
                </span>
                <strong>3</strong>
              </div>
              <div>
                <span>
                  <i className="is-blocked" /> Blocked
                </span>
                <strong>2</strong>
              </div>
            </div>
          </div>

          <div className="risk-callout">
            <CircleAlert aria-hidden="true" size={18} />
            <div>
              <strong>Registration API may affect two tasks.</strong>
              <span>Resolve the blocked dependency before Oct 18.</span>
            </div>
            <button type="button">Review risk</button>
          </div>
        </section>

        <section className="dashboard-panel dashboard-panel--deadline">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Next milestone</p>
              <h2>Core workflow demo</h2>
            </div>
            <CalendarDays aria-hidden="true" size={20} />
          </div>
          <p className="deadline-date">October 24</p>
          <div
            className="deadline-progress"
            aria-label="Milestone is 7 days away"
          >
            <span style={{ width: '58%' }} />
          </div>
          <dl className="deadline-stats">
            <div>
              <dt>Remaining</dt>
              <dd>7 days</dd>
            </div>
            <div>
              <dt>Planned</dt>
              <dd>12 tasks</dd>
            </div>
          </dl>
        </section>

        <section className="dashboard-panel dashboard-panel--tasks">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Focus</p>
              <h2>My priority tasks</h2>
            </div>
            <Link to="/my-tasks">View all</Link>
          </div>
          <div className="priority-list">
            {priorityTasks.map((task) => (
              <article className="priority-task" key={task.code}>
                <div>
                  <span className="priority-task__code">{task.code}</span>
                  <h3>{task.title}</h3>
                </div>
                <span className={`status-chip is-${task.tone}`}>
                  {task.status}
                </span>
                <span className="priority-task__due">
                  <Clock3 aria-hidden="true" size={14} />
                  {task.due}
                </span>
              </article>
            ))}
          </div>
        </section>

        <section className="dashboard-panel dashboard-panel--activity">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Team pulse</p>
              <h2>Recent activity</h2>
            </div>
            <span className="live-indicator">
              <i /> Live
            </span>
          </div>
          <ol className="activity-list">
            {recentActivity.map((activity) => (
              <li key={`${activity.actor}-${activity.time}`}>
                <span className="activity-avatar" aria-hidden="true">
                  {activity.initials}
                </span>
                <p>
                  <strong>{activity.actor}</strong> {activity.action}
                  <time>{activity.time}</time>
                </p>
              </li>
            ))}
          </ol>
        </section>
      </div>
    </div>
  )
}

export default DashboardPage
