import { isRouteErrorResponse, Link, useRouteError } from 'react-router-dom'

export function RouteErrorBoundary() {
  const error = useRouteError()
  const title = isRouteErrorResponse(error)
    ? `${error.status} · ${error.statusText}`
    : 'This page could not be loaded.'

  return (
    <main className="system-state system-state--fullscreen">
      <div className="system-state__mark" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Route error</p>
      <h1>{title}</h1>
      <p>Return to the project overview and try the action again.</p>
      <Link className="system-state__action" to="/">
        Back to dashboard
      </Link>
    </main>
  )
}
