import { useId, type ReactNode } from 'react'
import { CircleAlert } from 'lucide-react'

import { classNames } from '@/lib/classNames'

export type SystemStateTone = 'error' | 'permission' | 'offline'

export interface SystemStateProps {
  eyebrow: string
  title: string
  description: string
  primaryAction?: ReactNode
  secondaryAction?: ReactNode
  icon?: ReactNode
  tone?: SystemStateTone
  fullScreen?: boolean
  className?: string
}

export function SystemState({
  eyebrow,
  title,
  description,
  primaryAction,
  secondaryAction,
  icon = <CircleAlert size={28} />,
  tone = 'error',
  fullScreen = false,
  className,
}: SystemStateProps) {
  const titleId = useId()
  const descriptionId = useId()

  return (
    <section
      className={classNames(
        'system-state',
        `system-state--${tone}`,
        fullScreen && 'system-state--fullscreen',
        className,
      )}
      role="alert"
      aria-live="assertive"
      aria-labelledby={titleId}
      aria-describedby={descriptionId}
    >
      <div className="system-state__mark" aria-hidden="true">
        {icon}
      </div>
      <p className="eyebrow">{eyebrow}</p>
      <h1 id={titleId}>{title}</h1>
      <p id={descriptionId}>{description}</p>
      {(primaryAction || secondaryAction) && (
        <div className="system-state__actions">
          {primaryAction}
          {secondaryAction}
        </div>
      )}
    </section>
  )
}
