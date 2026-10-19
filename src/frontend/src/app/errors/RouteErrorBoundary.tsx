import { isRouteErrorResponse, Link, useRouteError } from 'react-router-dom'

import { SystemState } from '@/components/ui/SystemState/SystemState'

export function RouteErrorBoundary() {
  const error = useRouteError()
  const title = isRouteErrorResponse(error)
    ? `${error.status} · ${error.statusText}`
    : 'This page could not be loaded.'

  return (
    <main>
      <SystemState
        fullScreen
        eyebrow="Route error"
        title={title}
        description="Return to the project overview and try the action again."
        primaryAction={
          <Link className="system-state__action" to="/">
            Back to dashboard
          </Link>
        }
      />
    </main>
  )
}
