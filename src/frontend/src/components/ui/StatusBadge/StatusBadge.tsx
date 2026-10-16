import {
  Archive,
  Ban,
  Circle,
  CircleCheck,
  Clock3,
  Link2,
  RotateCcw,
  SearchCheck,
} from 'lucide-react'

import { classNames } from '@/lib/classNames'

export type TaskStatus =
  | 'todo'
  | 'in-progress'
  | 'evidence-linked'
  | 'in-review'
  | 'needs-changes'
  | 'blocked'
  | 'done'
  | 'archived'

const statusConfiguration = {
  todo: { label: 'Todo', icon: Circle },
  'in-progress': { label: 'In progress', icon: Clock3 },
  'evidence-linked': { label: 'Evidence linked', icon: Link2 },
  'in-review': { label: 'In review', icon: SearchCheck },
  'needs-changes': { label: 'Needs changes', icon: RotateCcw },
  blocked: { label: 'Blocked', icon: Ban },
  done: { label: 'Done', icon: CircleCheck },
  archived: { label: 'Archived', icon: Archive },
} satisfies Record<TaskStatus, { label: string; icon: typeof Circle }>

export interface StatusBadgeProps {
  status: TaskStatus
  label?: string
  className?: string
}

export function StatusBadge({ status, label, className }: StatusBadgeProps) {
  const configuration = statusConfiguration[status]
  const Icon = configuration.icon

  return (
    <span
      className={classNames(
        'ui-status-badge',
        `ui-status-badge--${status}`,
        className,
      )}
    >
      <Icon aria-hidden="true" size={13} strokeWidth={2.2} />
      {label ?? configuration.label}
    </span>
  )
}
