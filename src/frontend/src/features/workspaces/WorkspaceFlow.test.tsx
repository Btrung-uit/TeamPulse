import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { AuthContext, type AuthContextValue } from '@/features/auth/AuthContext'
import { WorkspaceOnboardingPage } from '@/features/workspaces/pages/WorkspaceOnboardingPage'
import { WorkspaceMembersPage } from '@/features/workspaces/pages/WorkspaceMembersPage'
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

afterEach(() => {
  vi.unstubAllGlobals()
  window.localStorage.clear()
})

function workspaceContext(
  overrides: Partial<WorkspaceContextValue> = {},
): WorkspaceContextValue {
  return {
    status: 'ready',
    workspaces: [],
    currentWorkspace: null,
    error: null,
    createWorkspace: vi.fn(),
    selectWorkspace: vi.fn(),
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

  it('lets a new account wait for a Leader instead of creating a workspace', async () => {
    const user = userEvent.setup()
    const reload = vi.fn().mockResolvedValue(undefined)

    render(
      <AuthContext.Provider value={authenticatedUser}>
        <WorkspaceContext.Provider value={workspaceContext({ reload })}>
          <MemoryRouter>
            <WorkspaceOnboardingPage />
          </MemoryRouter>
        </WorkspaceContext.Provider>
      </AuthContext.Provider>,
    )

    await user.click(
      screen.getByRole('button', { name: 'Join an existing workspace' }),
    )

    expect(screen.getByText('leader@example.com')).toBeInTheDocument()
    await user.click(
      screen.getByRole('button', { name: "I've contacted my Leader" }),
    )
    expect(
      screen.getByRole('heading', { name: 'Waiting for workspace access' }),
    ).toBeInTheDocument()
    await user.click(
      screen.getByRole('button', { name: 'Check workspace access' }),
    )
    expect(reload).toHaveBeenCalledOnce()
    expect(
      await screen.findByText(/No workspace access was found yet/),
    ).toBeInTheDocument()
  })

  it('restores the waiting state for the signed-in account', () => {
    window.localStorage.setItem(
      'teampulse-workspace-waiting:leader@example.com',
      'true',
    )

    render(
      <AuthContext.Provider value={authenticatedUser}>
        <WorkspaceContext.Provider value={workspaceContext()}>
          <MemoryRouter>
            <WorkspaceOnboardingPage />
          </MemoryRouter>
        </WorkspaceContext.Provider>
      </AuthContext.Provider>,
    )

    expect(
      screen.getByRole('heading', { name: 'Waiting for workspace access' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(/TeamPulse has not sent a request/),
    ).toBeInTheDocument()
  })

  it('lets the user leave the waiting state and review joining steps', async () => {
    window.localStorage.setItem(
      'teampulse-workspace-waiting:leader@example.com',
      'true',
    )
    const user = userEvent.setup()

    render(
      <AuthContext.Provider value={authenticatedUser}>
        <WorkspaceContext.Provider value={workspaceContext()}>
          <MemoryRouter>
            <WorkspaceOnboardingPage />
          </MemoryRouter>
        </WorkspaceContext.Provider>
      </AuthContext.Provider>,
    )

    await user.click(
      screen.getByRole('button', { name: 'Back to joining steps' }),
    )

    expect(
      screen.getByRole('heading', { name: 'Join an existing workspace' }),
    ).toBeInTheDocument()
    expect(
      window.localStorage.getItem(
        'teampulse-workspace-waiting:leader@example.com',
      ),
    ).toBeNull()
  })

  it('lets a Leader add an existing account from the Members page', async () => {
    const user = userEvent.setup()
    const reload = vi.fn().mockResolvedValue(undefined)
    const leaderWorkspace: WorkspaceSummary = {
      id: 'workspace-1',
      name: 'UIT Engineering',
      role: 'Leader',
      memberCount: 1,
    }
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify([
            {
              userId: 'user-1',
              email: 'leader@example.com',
              displayName: 'Team Leader',
              role: 'Leader',
            },
          ]),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        ),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            userId: 'user-2',
            email: 'member@example.com',
            displayName: 'Team Member',
            role: 'Member',
          }),
          { status: 201, headers: { 'Content-Type': 'application/json' } },
        ),
      )
    vi.stubGlobal('fetch', fetchMock)

    render(
      <AuthContext.Provider value={authenticatedUser}>
        <WorkspaceContext.Provider
          value={workspaceContext({
            workspaces: [leaderWorkspace],
            currentWorkspace: leaderWorkspace,
            reload,
          })}
        >
          <WorkspaceMembersPage />
        </WorkspaceContext.Provider>
      </AuthContext.Provider>,
    )

    expect(await screen.findByText('Team Leader')).toBeInTheDocument()
    await user.type(
      screen.getByLabelText('Registered email'),
      'member@example.com',
    )
    await user.click(screen.getByRole('button', { name: 'Add member' }))

    expect(await screen.findByText('Team Member')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/workspaces/workspace-1/members',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          email: 'member@example.com',
          role: 'Member',
        }),
      }),
    )
    expect(reload).toHaveBeenCalledOnce()
  })
})
