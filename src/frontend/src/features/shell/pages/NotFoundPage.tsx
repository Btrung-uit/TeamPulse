import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <section className="placeholder-page" aria-labelledby="not-found-title">
      <div className="placeholder-page__icon" aria-hidden="true">
        404
      </div>
      <p className="eyebrow">Page not found</p>
      <h1 id="not-found-title">This route is not part of TeamPulse.</h1>
      <p>Use the project navigation or return to the dashboard.</p>
      <Link className="system-state__action" to="/">
        Back to dashboard
      </Link>
    </section>
  )
}
