import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'

import { AuthContext, type AuthContextValue } from '@/features/auth/AuthContext'
import { WorkspaceOnboardingPage } from '@/features/workspaces/pages/WorkspaceOnboardingPage'
import type { WorkspaceSummary } from '@/features/workspaces/types'
import {
  WorkspaceContext,
  type WorkspaceContextValue,
} from '@/features/workspaces/WorkspaceContext'
import { WorkspaceRequiredRoute } from '@/features/workspaces/WorkspaceRoutes'

const authenticatedUser: AuthContextValue = {
  status: 'authenticated',
  user: {
    id: 'user-1',
    email: 'leader@example.com',
    displayName: 'Team Leader',
  },
  accessToken: 'test-token',
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}

function workspaceContext(
  overrides: Partial<WorkspaceContextValue> = {},
): WorkspaceContextValue {
  return {
    status: 'ready',
    workspaces: [],
    currentWorkspace: null,
    error: null,
    createWorkspace: vi.fn(),
    reload: vi.fn(),
    ...overrides,
  }
}

describe('workspace onboarding flow', () => {
  it('redirects an authenticated user without a workspace to onboarding', async () => {
    render(
      <WorkspaceContext.Provider value={workspaceContext()}>
        <MemoryRouter initialEntries={['/']}>
          <Routes>
            <Route element={<WorkspaceRequiredRoute />}>
              <Route path="/" element={<div>Private workspace</div>} />
            </Route>
            <Route
              path="/onboarding/workspace"
              element={<div>Workspace onboarding</div>}
            />
          </Routes>
        </MemoryRouter>
      </WorkspaceContext.Provider>,
    )

    expect(await screen.findByText('Workspace onboarding')).toBeInTheDocument()
    expect(screen.queryByText('Private workspace')).not.toBeInTheDocument()
  })

  it('creates the first workspace and continues to the app', async () => {
    const user = userEvent.setup()
    const workspace: WorkspaceSummary = {
      id: 'workspace-1',
      name: 'UIT Engineering',
      role: 'Leader',
      memberCount: 1,
    }
    const createWorkspace = vi.fn().mockResolvedValue(workspace)

    render(
      <AuthContext.Provider value={authenticatedUser}>
        <WorkspaceContext.Provider
          value={workspaceContext({ createWorkspace })}
        >
          <MemoryRouter initialEntries={['/onboarding/workspace']}>
            <Routes>
              <Route
                path="/onboarding/workspace"
                element={<WorkspaceOnboardingPage />}
              />
              <Route path="/" element={<div>Workspace dashboard</div>} />
            </Routes>
          </MemoryRouter>
        </WorkspaceContext.Provider>
      </AuthContext.Provider>,
    )

    await user.type(
      screen.getByLabelText('Workspace name'),
      '  UIT Engineering  ',
    )
    await user.click(screen.getByRole('button', { name: 'Create workspace' }))

    expect(createWorkspace).toHaveBeenCalledWith('UIT Engineering')
    expect(await screen.findByText('Workspace dashboard')).toBeInTheDocument()
  })
})
