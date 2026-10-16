import {
  CalendarDays,
  GitCommitHorizontal,
  MoreHorizontal,
  Paperclip,
} from 'lucide-react'

import { Button } from '@/components/ui/Button/Button'
import {
  StatusBadge,
  type TaskStatus,
} from '@/components/ui/StatusBadge/StatusBadge'
import { classNames } from '@/lib/classNames'

export type TaskPriority = 'low' | 'medium' | 'high' | 'urgent'

export interface TaskEvidenceMetric {
  type: 'commit' | 'attachment'
  count: number
  label: string
}

export interface TaskCardProps {
  code: string
  title: string
  status: TaskStatus
  priority: TaskPriority
  assignee: {
    name: string
    initials: string
  }
  dueDate: string
  evidence?: TaskEvidenceMetric[]
  selected?: boolean
  dragging?: boolean
  overdue?: boolean
  blocked?: boolean
  archived?: boolean
  skeleton?: boolean
  onClick?: () => void
  onMenuClick?: () => void
}

const priorityLabels: Record<TaskPriority, string> = {
  low: 'Low priority',
  medium: 'Medium priority',
  high: 'High priority',
  urgent: 'Urgent priority',
}

function EvidenceMetric({ metric }: { metric: TaskEvidenceMetric }) {
  const Icon = metric.type === 'commit' ? GitCommitHorizontal : Paperclip

  return (
    <span className="ui-task-card__metric" title={metric.label}>
      <Icon aria-hidden="true" size={14} />
      <span>{metric.count}</span>
      <span className="sr-only">{metric.label}</span>
    </span>
  )
}

export function TaskCard({
  code,
  title,
  status,
  priority,
  assignee,
  dueDate,
  evidence = [],
  selected = false,
  dragging = false,
  overdue = false,
  blocked = false,
  archived = false,
  skeleton = false,
  onClick,
  onMenuClick,
}: TaskCardProps) {
  if (skeleton) {
    return (
      <article
        className="ui-task-card ui-task-card--skeleton"
        aria-busy="true"
        aria-label="Loading task"
      >
        <span />
        <span />
        <span />
      </article>
    )
  }

  return (
    <article
      className={classNames(
        'ui-task-card',
        selected && 'is-selected',
        dragging && 'is-dragging',
        overdue && 'is-overdue',
        blocked && 'is-blocked',
        archived && 'is-archived',
        onClick && 'is-interactive',
      )}
    >
      {onClick && (
        <button
          className="ui-task-card__hit-target"
          type="button"
          aria-label={`Open ${code}: ${title}`}
          aria-pressed={selected}
          onClick={onClick}
        />
      )}
      <div className="ui-task-card__topline">
        <span className="ui-task-card__code">{code}</span>
        {onMenuClick && (
          <span
            className="ui-task-card__menu"
            onClick={(event) => event.stopPropagation()}
            onKeyDown={(event) => event.stopPropagation()}
          >
            <Button
              iconOnly
              size="sm"
              variant="ghost"
              aria-label={`Open actions for ${code}`}
              title="Task actions"
              onClick={onMenuClick}
            >
              <MoreHorizontal aria-hidden="true" size={18} />
            </Button>
          </span>
        )}
      </div>

      <h3>{title}</h3>

      <div className="ui-task-card__classification">
        <StatusBadge status={status} />
        <span
          className={`ui-priority ui-priority--${priority}`}
          aria-label={priorityLabels[priority]}
        >
          <i aria-hidden="true" />
          {priorityLabels[priority].replace(' priority', '')}
        </span>
      </div>

      {(overdue || blocked) && (
        <p className="ui-task-card__alert">
          {blocked ? 'Blocked dependency' : 'Overdue task'}
        </p>
      )}

      <footer className="ui-task-card__footer">
        <span className="ui-task-card__assignee" title={assignee.name}>
          <span aria-hidden="true">{assignee.initials}</span>
          <span className="sr-only">Assigned to {assignee.name}</span>
        </span>
        <span className="ui-task-card__due">
          <CalendarDays aria-hidden="true" size={14} />
          {dueDate}
        </span>
        {evidence.slice(0, 2).map((metric) => (
          <EvidenceMetric
            key={`${metric.type}-${metric.label}`}
            metric={metric}
          />
        ))}
      </footer>
    </article>
  )
}
