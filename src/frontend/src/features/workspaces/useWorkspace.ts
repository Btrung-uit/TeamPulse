import { useContext } from 'react'

import { WorkspaceContext } from '@/features/workspaces/WorkspaceContext'

export function useWorkspace() {
  const context = useContext(WorkspaceContext)

  if (!context) {
    throw new Error('useWorkspace must be used inside WorkspaceProvider.')
  }

  return context
}
