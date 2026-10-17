import { useMemo, useState } from 'react'
import {
  ArrowDownAZ,
  CalendarClock,
  FilterX,
  ListFilter,
  Plus,
  Search,
  Upload,
} from 'lucide-react'
import { Link, useSearchParams } from 'react-router-dom'

import { Button } from '@/components/ui/Button/Button'
import { EmptyState } from '@/components/ui/EmptyState/EmptyState'
import { Input } from '@/components/ui/Input/Input'
import { TaskCard } from '@/components/ui/TaskCard/TaskCard'
import { useProject } from '@/features/projects/useProject'
import { CreateTaskModal } from '@/features/tasks/components/CreateTaskModal'
import { TaskSelectField as SelectField } from '@/features/tasks/components/TaskSelectField'
import {
  dueLabel,
  isOverdue,
  memberInitials,
  priorityForCard,
  readPriority,
  readSort,
  sortTasks,
  statusForCard,
  taskColumns,
} from '@/features/tasks/taskBoardModel'
import { useTaskOptions, useTasks } from '@/features/tasks/taskQueries'
import type { TaskFilters } from '@/features/tasks/types'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

function TaskBoardSkeleton() {
  return (
    <div className="task-board task-board--loading" aria-label="Loading tasks">
      {taskColumns.slice(0, 4).map((column) => (
        <section className="task-column" key={column.status}>
          <div className="task-column__heading">
            <span>{column.label}</span>
          </div>
          <TaskCard
            skeleton
            code=""
            title=""
            status="todo"
            priority="medium"
            assignee={{ name: '', initials: '' }}
            dueDate=""
          />
          <TaskCard
            skeleton
            code=""
            title=""
            status="todo"
            priority="medium"
            assignee={{ name: '', initials: '' }}
            dueDate=""
          />
        </section>
      ))}
    </div>
  )
}

export function TaskBoardPage() {
  const { currentWorkspace } = useWorkspace()
  const { currentProject } = useProject()
  const [searchParams, setSearchParams] = useSearchParams()
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const workspaceId = currentWorkspace!.id
  const projectId = currentProject!.id
  const canManage = currentWorkspace?.role === 'Leader'
  const projectBase = `/workspaces/${workspaceId}/projects/${projectId}`
  const sort = readSort(searchParams.get('sort'))
  const filters = useMemo<TaskFilters>(
    () => ({
      search: searchParams.get('search')?.trim() || undefined,
      priority: readPriority(searchParams.get('priority')),
      assigneeUserId: searchParams.get('assignee') || undefined,
      sprintId: searchParams.get('sprint') || undefined,
    }),
    [searchParams],
  )
  const tasksQuery = useTasks(workspaceId, projectId, filters)
  const options = useTaskOptions(workspaceId, projectId)
  const tasks = useMemo(
    () => sortTasks(tasksQuery.data ?? [], sort),
    [sort, tasksQuery.data],
  )
  const hasFilters = Boolean(
    filters.search ||
    filters.priority ||
    filters.assigneeUserId ||
    filters.sprintId,
  )

  function setQueryParam(name: string, value: string) {
    setSearchParams(
      (current) => {
        const next = new URLSearchParams(current)
        if (value) next.set(name, value)
        else next.delete(name)
        return next
      },
      { replace: true },
    )
  }

  function clearFilters() {
    setSearchParams((current) => {
      const next = new URLSearchParams(current)
      ;['search', 'priority', 'assignee', 'sprint'].forEach((key) =>
        next.delete(key),
      )
      return next
    })
  }

  const memberOptions = options.members.data ?? []
  const sprintOptions = options.sprints.data ?? []

  return (
    <div className="task-board-page">
      <header className="task-board-header">
        <div>
          <p className="eyebrow">{currentProject!.name}</p>
          <h1>Task board</h1>
          <p>Plan the workflow, spot bottlenecks and keep ownership visible.</p>
        </div>
        {canManage && (
          <Button
            leadingIcon={<Plus size={17} />}
            onClick={() => setIsCreateOpen(true)}
          >
            New task
          </Button>
        )}
      </header>

      <section className="task-toolbar" aria-label="Task board filters">
        <Input
          label="Search tasks"
          placeholder="Code or title"
          startIcon={<Search size={17} />}
          value={searchParams.get('search') ?? ''}
          onChange={(event) => setQueryParam('search', event.target.value)}
        />
        <SelectField
          label="Assignee"
          name="assignee-filter"
          value={filters.assigneeUserId ?? ''}
          onChange={(event) => setQueryParam('assignee', event.target.value)}
        >
          <option value="">All assignees</option>
          {memberOptions.map((member) => (
            <option key={member.userId} value={member.userId}>
              {member.displayName}
            </option>
          ))}
        </SelectField>
        <SelectField
          label="Sprint"
          name="sprint-filter"
          value={filters.sprintId ?? ''}
          onChange={(event) => setQueryParam('sprint', event.target.value)}
        >
          <option value="">All sprints</option>
          {sprintOptions.map((sprint) => (
            <option key={sprint.id} value={sprint.id}>
              {sprint.name}
            </option>
          ))}
        </SelectField>
        <SelectField
          label="Priority"
          name="priority-filter"
          value={filters.priority ?? ''}
          onChange={(event) => setQueryParam('priority', event.target.value)}
        >
          <option value="">All priorities</option>
          <option value="Low">Low</option>
          <option value="Medium">Medium</option>
          <option value="High">High</option>
          <option value="Urgent">Urgent</option>
        </SelectField>
        <SelectField
          label="Sort"
          name="task-sort"
          value={sort}
          onChange={(event) => setQueryParam('sort', event.target.value)}
        >
          <option value="due-asc">Deadline: soonest</option>
          <option value="updated-desc">Recently updated</option>
          <option value="code-asc">Task code</option>
          <option value="priority-desc">Priority: highest</option>
        </SelectField>
        {hasFilters && (
          <Button
            variant="ghost"
            leadingIcon={<FilterX size={16} />}
            onClick={clearFilters}
          >
            Clear
          </Button>
        )}
      </section>

      <div className="task-board-summary" aria-live="polite">
        <span>
          <ListFilter aria-hidden="true" size={15} />
          {tasks.length} {tasks.length === 1 ? 'task' : 'tasks'} shown
        </span>
        <span>
          <ArrowDownAZ aria-hidden="true" size={15} />
          Filters stay in this project URL
        </span>
      </div>

      {tasksQuery.isLoading ? (
        <TaskBoardSkeleton />
      ) : tasksQuery.isError ? (
        <section className="task-board-error" role="alert">
          <p className="eyebrow">Connection issue</p>
          <h2>We could not load this task board.</h2>
          <p>The current filters are safe. Retry when the API is available.</p>
          <Button onClick={() => void tasksQuery.refetch()}>Try again</Button>
        </section>
      ) : tasks.length === 0 ? (
        <EmptyState
          className="task-board-empty"
          icon={hasFilters ? <Search size={24} /> : <CalendarClock size={24} />}
          title={hasFilters ? 'No tasks match these filters' : 'No tasks yet'}
          description={
            hasFilters
              ? 'Clear one or more filters to return to the full project board.'
              : canManage
                ? 'Create the first task now, or start from an Excel plan when the import workflow is available.'
                : 'A workspace leader has not created any tasks for this project yet.'
          }
          variant={hasFilters ? 'filtered' : 'first-use'}
          primaryAction={
            hasFilters ? (
              <Button variant="secondary" onClick={clearFilters}>
                Clear filters
              </Button>
            ) : canManage ? (
              <Button
                leadingIcon={<Plus size={17} />}
                onClick={() => setIsCreateOpen(true)}
              >
                New task
              </Button>
            ) : undefined
          }
          secondaryAction={
            !hasFilters && canManage ? (
              <Link
                className="ui-button ui-button--secondary ui-button--md"
                to={`${projectBase}/import`}
              >
                <Upload aria-hidden="true" size={17} />
                Import Excel
              </Link>
            ) : undefined
          }
        />
      ) : (
        <div className="task-board" tabIndex={0} aria-label="Kanban task board">
          {taskColumns.map((column) => {
            const columnTasks = tasks.filter(
              (task) => task.status === column.status,
            )

            return (
              <section
                className="task-column"
                key={column.status}
                aria-labelledby={`task-column-${column.status}`}
              >
                <div className="task-column__heading">
                  <span id={`task-column-${column.status}`}>
                    {column.label}
                  </span>
                  <strong>{columnTasks.length}</strong>
                </div>
                <div className="task-column__cards">
                  {columnTasks.length === 0 ? (
                    <p className="task-column__empty">No tasks</p>
                  ) : (
                    columnTasks.map((task) => {
                      const assignee =
                        task.assignee?.displayName ?? 'Unassigned'
                      return (
                        <TaskCard
                          key={task.id}
                          code={task.code}
                          title={task.title}
                          status={statusForCard[task.status]}
                          priority={priorityForCard[task.priority]}
                          assignee={{
                            name: assignee,
                            initials: task.assignee
                              ? memberInitials(assignee)
                              : '—',
                          }}
                          dueDate={dueLabel(task.dueOn)}
                          overdue={isOverdue(task)}
                          blocked={task.status === 'Blocked'}
                        />
                      )
                    })
                  )}
                </div>
              </section>
            )
          })}
        </div>
      )}

      <CreateTaskModal
        isOpen={isCreateOpen}
        workspaceId={workspaceId}
        projectId={projectId}
        onClose={() => setIsCreateOpen(false)}
      />
    </div>
  )
}
