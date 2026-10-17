import type { ReactNode, SelectHTMLAttributes } from 'react'

interface TaskSelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string
  children: ReactNode
  error?: string
}

export function TaskSelectField({
  label,
  children,
  error,
  id,
  ...props
}: TaskSelectFieldProps) {
  const fieldId = id ?? `task-${props.name}`

  return (
    <div className={`task-select-field${error ? ' has-error' : ''}`}>
      <label htmlFor={fieldId}>{label}</label>
      <select
        {...props}
        id={fieldId}
        aria-invalid={Boolean(error) || undefined}
      >
        {children}
      </select>
      {error && <p role="alert">{error}</p>}
    </div>
  )
}
