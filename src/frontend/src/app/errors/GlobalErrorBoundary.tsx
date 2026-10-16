import { Component, type ErrorInfo, type PropsWithChildren } from 'react'

interface GlobalErrorBoundaryState {
  hasError: boolean
}

export class GlobalErrorBoundary extends Component<
  PropsWithChildren,
  GlobalErrorBoundaryState
> {
  public state: GlobalErrorBoundaryState = { hasError: false }

  public static getDerivedStateFromError(): GlobalErrorBoundaryState {
    return { hasError: true }
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('Unhandled application error', error, errorInfo)
  }

  public render() {
    if (this.state.hasError) {
      return (
        <main className="system-state system-state--fullscreen">
          <div className="system-state__mark" aria-hidden="true">
            !
          </div>
          <p className="eyebrow">Unexpected error</p>
          <h1>TeamPulse could not continue.</h1>
          <p>Reload the application. Your project data has not been changed.</p>
          <button type="button" onClick={() => window.location.reload()}>
            Reload application
          </button>
        </main>
      )
    }

    return this.props.children
  }
}
