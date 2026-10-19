import { useMemo } from 'react'
import {
  AlertTriangle,
  CalendarClock,
  CheckCircle2,
  CircleSlash2,
  Search,
  UserRound,
} from 'lucide-react'
import { Link, useSearchParams } from 'react-router-dom'

import { Button } from '@/components/ui/Button/Button'
import { EmptyState } from '@/components/ui/EmptyState/EmptyState'
import { Input } from '@/components/ui/Input/Input'
import { TaskCard } from '@/components/ui/TaskCard/TaskCard'
import { useAuth } from '@/features/auth/useAuth'
import { useProject } from '@/features/projects/useProject'
import { TaskDetailPanel } from '@/features/tasks/components/TaskDetailPanel'
import { TaskSelectField } from '@/features/tasks/components/TaskSelectField'
import {
  filterAndSortMyTasks,
  isMyTaskOverdue,
  needsAttention,
  readMyTaskDueFilter,
  readMyTaskStatusFilter,
  summarizeMyTasks,
} from '@/features/tasks/myTasksModel'
import {
  dueLabel,
  memberInitials,
  priorityForCard,
  statusForCard,
} from '@/features/tasks/taskBoardModel'
import { useTasks } from '@/features/tasks/taskQueries'
import type { TaskSummary } from '@/features/tasks/types'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

function TaskListSkeleton() {
  return (
    <div
      className="my-task-grid"
      role="status"
      aria-live="polite"
      aria-label="Loading your tasks"
    >
      {Array.from({ length: 4 }, (_, index) => (
        <TaskCard
          key={index}
          skeleton
          code=""
          title=""
          status="todo"
          priority="medium"
          assignee={{ name: '', initials: '' }}
          dueDate=""
        />
      ))}
    </div>
  )
}

function MyTaskCard({
  task,
  selected,
  onOpen,
}: {
  task: TaskSummary
  selected: boolean
  onOpen: () => void
}) {
  const assignee = task.assignee?.displayName ?? 'Unassigned'

  return (
    <TaskCard
      code={task.code}
      title={task.title}
      status={statusForCard[task.status]}
      priority={priorityForCard[task.priority]}
      assignee={{
        name: assignee,
        initials: task.assignee ? memberInitials(assignee) : '—',
      }}
      dueDate={dueLabel(task.dueOn)}
      overdue={isMyTaskOverdue(task)}
      blocked={task.status === 'Blocked'}
      selected={selected}
      onClick={onOpen}
    />
  )
}

export function MyTasksPage() {
  const { user } = useAuth()
  const { currentWorkspace } = useWorkspace()
  const { currentProject } = useProject()
  const [searchParams, setSearchParams] = useSearchParams()
  const workspaceId = currentWorkspace!.id
  const projectId = currentProject!.id
  const selectedTaskId = searchParams.get('task') || undefined
  const search = searchParams.get('search') ?? ''
  const status = readMyTaskStatusFilter(searchParams.get('status'))
  const due = readMyTaskDueFilter(searchParams.get('due'))
  const tasksQuery = useTasks(workspaceId, projectId, {
    assigneeUserId: user!.id,
  })
  const tasks = useMemo(() => tasksQuery.data ?? [], [tasksQuery.data])
  const summary = useMemo(() => summarizeMyTasks(tasks), [tasks])
  const visibleTasks = useMemo(
    () => filterAndSortMyTasks(tasks, { search, status, due }),
    [due, search, status, tasks],
  )
  const attentionTasks = visibleTasks.filter((task) => needsAttention(task))
  const nextTasks = visibleTasks.filter((task) => !needsAttention(task))
  const hasFilters = Boolean(search || status !== 'active' || due !== 'all')
  const projectBase = `/workspaces/${workspaceId}/projects/${projectId}`

  function setQueryParam(name: string, value: string, defaultValue?: string) {
    setSearchParams(
      (current) => {
        const next = new URLSearchParams(current)
        if (value && value !== defaultValue) next.set(name, value)
        else next.delete(name)
        return next
      },
      { replace: true },
    )
  }

  function selectTask(taskId?: string) {
    setSearchParams((current) => {
      const next = new URLSearchParams(current)
      if (taskId) next.set('task', taskId)
      else next.delete('task')
      return next
    })
  }

  function clearFilters() {
    setSearchParams((current) => {
      const next = new URLSearchParams(current)
      ;['search', 'status', 'due'].forEach((key) => next.delete(key))
      return next
    })
  }

  return (
    <div className="my-tasks-page">
      <header className="my-tasks-header">
        <div>
          <p className="eyebrow">{currentProject!.name}</p>
          <h1>My tasks</h1>
          <p>
            See what needs attention and update your personal workflow safely.
          </p>
        </div>
        <div
          className="my-tasks-assignee"
          aria-label={`Assigned to ${user!.displayName}`}
        >
          <span aria-hidden="true">
            <UserRound size={17} />
          </span>
          <div>
            <small>Assigned to</small>
            <strong>{user!.displayName}</strong>
          </div>
        </div>
      </header>

      <section className="my-task-summary" aria-label="My task summary">
        <article>
          <CheckCircle2 aria-hidden="true" size={18} />
          <span>Active</span>
          <strong>{summary.active}</strong>
        </article>
        <article className={summary.blocked ? 'has-risk' : undefined}>
          <CircleSlash2 aria-hidden="true" size={18} />
          <span>Blocked</span>
          <strong>{summary.blocked}</strong>
        </article>
        <article className={summary.overdue ? 'has-risk' : undefined}>
          <AlertTriangle aria-hidden="true" size={18} />
          <span>Overdue</span>
          <strong>{summary.overdue}</strong>
        </article>
        <article>
          <CalendarClock aria-hidden="true" size={18} />
          <span>Due in 7 days</span>
          <strong>{summary.dueSoon}</strong>
        </article>
      </section>

      <section className="my-task-toolbar" aria-label="My task filters">
        <Input
          label="Search my tasks"
          placeholder="Code or title"
          startIcon={<Search size={17} />}
          value={search}
          onChange={(event) => setQueryParam('search', event.target.value)}
        />
        <TaskSelectField
          label="Status"
          name="my-task-status"
          value={status}
          onChange={(event) =>
            setQueryParam('status', event.target.value, 'active')
          }
        >
          <option value="active">Active only</option>
          <option value="all">All statuses</option>
          <option value="Todo">Todo</option>
          <option value="InProgress">In progress</option>
          <option value="EvidenceLinked">Evidence linked</option>
          <option value="InReview">In review</option>
          <option value="NeedsChanges">Needs changes</option>
          <option value="Blocked">Blocked</option>
          <option value="Done">Done</option>
        </TaskSelectField>
        <TaskSelectField
          label="Deadline"
          name="my-task-due"
          value={due}
          onChange={(event) => setQueryParam('due', event.target.value, 'all')}
        >
          <option value="all">Any deadline</option>
          <option value="overdue">Overdue</option>
          <option value="today">Due today</option>
          <option value="next-7-days">Due in 7 days</option>
          <option value="no-deadline">No deadline</option>
        </TaskSelectField>
        {hasFilters && (
          <Button variant="ghost" onClick={clearFilters}>
            Clear
          </Button>
        )}
      </section>

      <div className="my-task-result-count" aria-live="polite">
        {visibleTasks.length} {visibleTasks.length === 1 ? 'task' : 'tasks'}{' '}
        shown
      </div>

      {tasksQuery.isLoading ? (
        <TaskListSkeleton />
      ) : tasksQuery.isError ? (
        <section className="my-task-error" role="alert">
          <AlertTriangle aria-hidden="true" size={26} />
          <h2>We could not load your tasks.</h2>
          <p>Your filters are safe. Retry when the API is available.</p>
          <Button onClick={() => void tasksQuery.refetch()}>Try again</Button>
        </section>
      ) : visibleTasks.length === 0 ? (
        <EmptyState
          className="my-task-empty"
          icon={hasFilters ? <Search size={24} /> : <CheckCircle2 size={24} />}
          title={
            hasFilters
              ? 'No tasks match these filters'
              : 'Nothing assigned to you'
          }
          description={
            hasFilters
              ? 'Clear one or more filters to return to your active queue.'
              : 'When a task is assigned to you, it will appear here automatically.'
          }
          variant={hasFilters ? 'filtered' : 'first-use'}
          primaryAction={
            hasFilters ? (
              <Button variant="secondary" onClick={clearFilters}>
                Clear filters
              </Button>
            ) : undefined
          }
          secondaryAction={
            <Link
              className="ui-button ui-button--secondary ui-button--md"
              to={`${projectBase}/tasks`}
            >
              Open task board
            </Link>
          }
        />
      ) : (
        <div className="my-task-sections">
          {attentionTasks.length > 0 && (
            <section
              className="my-task-section"
              aria-labelledby="attention-heading"
            >
              <div className="my-task-section__heading">
                <div>
                  <p className="eyebrow">Priority queue</p>
                  <h2 id="attention-heading">Needs attention</h2>
                </div>
                <span>{attentionTasks.length}</span>
              </div>
              <div className="my-task-grid">
                {attentionTasks.map((task) => (
                  <MyTaskCard
                    key={task.id}
                    task={task}
                    selected={selectedTaskId === task.id}
                    onOpen={() => selectTask(task.id)}
                  />
                ))}
              </div>
            </section>
          )}
          {nextTasks.length > 0 && (
            <section className="my-task-section" aria-labelledby="next-heading">
              <div className="my-task-section__heading">
                <div>
                  <p className="eyebrow">Personal queue</p>
                  <h2 id="next-heading">Next up</h2>
                </div>
                <span>{nextTasks.length}</span>
              </div>
              <div className="my-task-grid">
                {nextTasks.map((task) => (
                  <MyTaskCard
                    key={task.id}
                    task={task}
                    selected={selectedTaskId === task.id}
                    onOpen={() => selectTask(task.id)}
                  />
                ))}
              </div>
            </section>
          )}
        </div>
      )}

      <TaskDetailPanel
        taskId={selectedTaskId}
        workspaceId={workspaceId}
        projectId={projectId}
        role={currentWorkspace!.role}
        managementMode={false}
        onClose={() => selectTask()}
      />
    </div>
  )
}
