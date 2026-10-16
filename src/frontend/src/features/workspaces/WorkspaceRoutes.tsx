import { useEffect } from 'react'
import { Link, Navigate, Outlet, useParams } from 'react-router-dom'

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

export function WorkspaceHomeRedirect() {
  const { currentWorkspace } = useWorkspace()

  return currentWorkspace ? (
    <Navigate to={`/workspaces/${currentWorkspace.id}/projects`} replace />
  ) : null
}

export function WorkspaceUrlRoute() {
  const { workspaceId } = useParams()
  const { workspaces, currentWorkspace, selectWorkspace } = useWorkspace()
  const requestedWorkspace = workspaces.find(
    (workspace) => workspace.id === workspaceId,
  )

  useEffect(() => {
    if (requestedWorkspace && currentWorkspace?.id !== requestedWorkspace.id) {
      selectWorkspace(requestedWorkspace.id)
    }
  }, [currentWorkspace?.id, requestedWorkspace, selectWorkspace])

  if (!requestedWorkspace) {
    return (
      <main className="workspace-state" role="alert">
        <p className="eyebrow">No permission</p>
        <h1>This workspace is not available.</h1>
        <p>
          The link may be invalid, or your active membership does not grant
          access to this workspace.
        </p>
        <Link className="system-state__action" to="/">
          Go to my workspace
        </Link>
      </main>
    )
  }

  return currentWorkspace?.id === requestedWorkspace.id ? (
    <Outlet />
  ) : (
    <RouteLoading />
  )
}
