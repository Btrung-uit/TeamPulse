import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren,
} from 'react'
import {
  CircleAlert,
  CircleCheck,
  Info,
  LoaderCircle,
  TriangleAlert,
  X,
} from 'lucide-react'

import { Button } from '@/components/ui/Button/Button'
import {
  ToastContext,
  type ToastInput,
  type ToastRecord,
  type ToastVariant,
} from '@/components/ui/Toast/toastContext'

const toastIcons = {
  success: CircleCheck,
  info: Info,
  warning: TriangleAlert,
  error: CircleAlert,
  progress: LoaderCircle,
} satisfies Record<ToastVariant, typeof Info>

function defaultDuration(toast: ToastRecord) {
  if (toast.duration !== undefined) return toast.duration
  if (toast.variant === 'warning') return 6000
  if (toast.variant === 'error' || toast.variant === 'progress') return null
  return 4000
}

interface ToastItemProps {
  toast: ToastRecord
  onDismiss: (id: string) => void
}

function ToastItem({ toast, onDismiss }: ToastItemProps) {
  const Icon = toastIcons[toast.variant]
  const duration = defaultDuration(toast)

  useEffect(() => {
    if (duration === null) return

    const timeoutId = window.setTimeout(() => onDismiss(toast.id), duration)
    return () => window.clearTimeout(timeoutId)
  }, [duration, onDismiss, toast.id])

  return (
    <article
      className={`ui-toast ui-toast--${toast.variant}`}
      role={toast.variant === 'error' ? 'alert' : 'status'}
    >
      <span className="ui-toast__icon" aria-hidden="true">
        <Icon size={18} />
      </span>
      <div className="ui-toast__content">
        <strong>{toast.title}</strong>
        {toast.description && <p>{toast.description}</p>}
        {toast.action && (
          <button
            className="ui-toast__action"
            type="button"
            onClick={() => {
              toast.action?.onClick()
              onDismiss(toast.id)
            }}
          >
            {toast.action.label}
          </button>
        )}
      </div>
      <Button
        iconOnly
        variant="ghost"
        size="sm"
        aria-label={`Dismiss ${toast.title} notification`}
        title="Dismiss notification"
        onClick={() => onDismiss(toast.id)}
      >
        <X aria-hidden="true" size={16} />
      </Button>
    </article>
  )
}

export function ToastProvider({ children }: PropsWithChildren) {
  const [toasts, setToasts] = useState<ToastRecord[]>([])

  const dismissToast = useCallback((id: string) => {
    setToasts((currentToasts) =>
      currentToasts.filter((toast) => toast.id !== id),
    )
  }, [])

  const showToast = useCallback((toast: ToastInput) => {
    const id = crypto.randomUUID()
    const nextToast: ToastRecord = {
      ...toast,
      id,
      variant: toast.variant ?? 'info',
    }

    setToasts((currentToasts) => [...currentToasts, nextToast].slice(-3))
    return id
  }, [])

  const value = useMemo(
    () => ({ showToast, dismissToast }),
    [dismissToast, showToast],
  )

  return (
    <ToastContext.Provider value={value}>
      {children}
      <aside
        className="ui-toast-viewport"
        aria-label="Notifications"
        aria-live="polite"
      >
        {toasts.map((toast) => (
          <ToastItem key={toast.id} toast={toast} onDismiss={dismissToast} />
        ))}
      </aside>
    </ToastContext.Provider>
  )
}
