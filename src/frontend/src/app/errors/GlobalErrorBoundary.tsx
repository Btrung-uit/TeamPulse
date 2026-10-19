import { Component, type ErrorInfo, type PropsWithChildren } from 'react'

import { SystemState } from '@/components/ui/SystemState/SystemState'

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
        <main>
          <SystemState
            fullScreen
            eyebrow="Unexpected error"
            title="TeamPulse could not continue."
            description="Reload the application. Your project data has not been changed."
            primaryAction={
              <button type="button" onClick={() => window.location.reload()}>
                Reload application
              </button>
            }
          />
        </main>
      )
    }

    return this.props.children
  }
}
