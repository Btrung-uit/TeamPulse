export function RouteLoading() {
  return (
    <div className="route-loading" role="status" aria-live="polite">
      <span className="sr-only">Loading page</span>
      <div className="route-loading__heading" />
      <div className="route-loading__grid">
        {Array.from({ length: 4 }, (_, index) => (
          <div className="route-loading__card" key={index} />
        ))}
      </div>
      <div className="route-loading__panel" />
    </div>
  )
}
