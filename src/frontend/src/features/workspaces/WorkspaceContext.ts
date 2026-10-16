import { createContext } from 'react'

import type { WorkspaceSummary } from '@/features/workspaces/types'

export type WorkspaceStatus = 'idle' | 'loading' | 'ready' | 'error'

export interface WorkspaceContextValue {
  status: WorkspaceStatus
  workspaces: WorkspaceSummary[]
  currentWorkspace: WorkspaceSummary | null
  error: string | null
  createWorkspace: (name: string) => Promise<WorkspaceSummary>
  reload: () => Promise<void>
}

export const WorkspaceContext = createContext<WorkspaceContextValue | null>(
  null,
)
