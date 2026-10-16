import type { PropsWithChildren } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'

import { queryClient } from '@/app/providers/queryClient'
import { ThemeProvider } from '@/app/providers/ThemeProvider'
import { ToastProvider } from '@/components/ui/Toast/ToastProvider'
import { AuthProvider } from '@/features/auth/AuthProvider'
import { WorkspaceProvider } from '@/features/workspaces/WorkspaceProvider'

export function AppProviders({ children }: PropsWithChildren) {
  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <AuthProvider>
            <WorkspaceProvider>{children}</WorkspaceProvider>
          </AuthProvider>
        </ToastProvider>
      </QueryClientProvider>
    </ThemeProvider>
  )
}
