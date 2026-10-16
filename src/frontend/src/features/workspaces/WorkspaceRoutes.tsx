import { Navigate, Outlet } from 'react-router-dom'

import { RouteLoading } from '@/app/components/RouteLoading'
import { Button } from '@/components/ui/Button/Button'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

export function WorkspaceRequiredRoute() {
  const { status, currentWorkspace, error, reload } = useWorkspace()

  if (status === 'idle' || status === 'loading') {
    return <RouteLoading />
  }

  if (status === 'error') {
    return (
      <main className="workspace-state" role="alert">
        <p className="eyebrow">Connection issue</p>
        <h1>We could not load your workspace.</h1>
        <p>{error}</p>
        <Button onClick={() => void reload()}>Try again</Button>
      </main>
    )
  }

  return currentWorkspace ? (
    <Outlet />
  ) : (
    <Navigate to="/onboarding/workspace" replace />
  )
}

export function WorkspaceOnboardingRoute() {
  const { status, currentWorkspace, error, reload } = useWorkspace()

  if (status === 'idle' || status === 'loading') {
    return <RouteLoading />
  }

  if (status === 'error') {
    return (
      <main className="workspace-state" role="alert">
        <p className="eyebrow">Connection issue</p>
        <h1>Workspace setup is temporarily unavailable.</h1>
        <p>{error}</p>
        <Button onClick={() => void reload()}>Try again</Button>
      </main>
    )
  }

  return currentWorkspace ? <Navigate to="/" replace /> : <Outlet />
}
