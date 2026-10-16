import { useId, type ReactNode } from 'react'

import { classNames } from '@/lib/classNames'

export type EmptyStateVariant =
  'first-use' | 'filtered' | 'integration' | 'permission' | 'archived'

export interface EmptyStateProps {
  icon: ReactNode
  title: string
  description: string
  primaryAction?: ReactNode
  secondaryAction?: ReactNode
  variant?: EmptyStateVariant
  className?: string
}

export function EmptyState({
  icon,
  title,
  description,
  primaryAction,
  secondaryAction,
  variant = 'first-use',
  className,
}: EmptyStateProps) {
  const titleId = useId()

  return (
    <section
      className={classNames(
        'ui-empty-state',
        `ui-empty-state--${variant}`,
        className,
      )}
      aria-labelledby={titleId}
    >
      <div className="ui-empty-state__icon" aria-hidden="true">
        {icon}
      </div>
      <h3 id={titleId}>{title}</h3>
      <p>{description}</p>
      {(primaryAction || secondaryAction) && (
        <div className="ui-empty-state__actions">
          {primaryAction}
          {secondaryAction}
        </div>
      )}
    </section>
  )
}
