import { createContext } from 'react'

export type ToastVariant = 'success' | 'info' | 'warning' | 'error' | 'progress'

export interface ToastAction {
  label: string
  onClick: () => void
}

export interface ToastInput {
  title: string
  description?: string
  variant?: ToastVariant
  duration?: number | null
  action?: ToastAction
}

export interface ToastRecord extends ToastInput {
  id: string
  variant: ToastVariant
}

export interface ToastContextValue {
  showToast: (toast: ToastInput) => string
  dismissToast: (id: string) => void
}

export const ToastContext = createContext<ToastContextValue | null>(null)
