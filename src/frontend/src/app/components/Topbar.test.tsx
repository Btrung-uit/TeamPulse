import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'

import { Topbar } from '@/app/components/Topbar'
import { ThemeContext } from '@/app/providers/themeContext'
import { AuthContext, type AuthContextValue } from '@/features/auth/AuthContext'
import {
  ProjectContext,
  type ProjectContextValue,
} from '@/features/projects/ProjectContext'
import {
  WorkspaceContext,
  type WorkspaceContextValue,
} from '@/features/workspaces/WorkspaceContext'

const workspaceContext: WorkspaceContextValue = {
  status: 'ready',
  workspaces: [],
  currentWorkspace: {
    id: 'workspace-1',
    name: 'Software Engineering',
    role: 'Leader',
    memberCount: 3,
  },
  error: null,
  createWorkspace: vi.fn(),
  selectWorkspace: vi.fn(),
  reload: vi.fn(),
}

const projectContext: ProjectContextValue = {
  status: 'ready',
  projects: [],
  currentProject: null,
  error: null,
  createProject: vi.fn(),
  updateProject: vi.fn(),
  archiveProject: vi.fn(),
  reload: vi.fn(),
}

function renderTopbar(logout: AuthContextValue['logout']) {
  const authContext: AuthContextValue = {
    status: 'authenticated',
    user: {
      id: 'user-1',
      email: 'leader@example.com',
      displayName: 'Team Leader',
    },
    accessToken: 'access-token',
    login: vi.fn(),
    register: vi.fn(),
    logout,
  }

  return render(
    <MemoryRouter>
      <ThemeContext.Provider value={{ theme: 'light', toggleTheme: vi.fn() }}>
        <AuthContext.Provider value={authContext}>
          <WorkspaceContext.Provider value={workspaceContext}>
            <ProjectContext.Provider value={projectContext}>
              <Topbar />
            </ProjectContext.Provider>
          </WorkspaceContext.Provider>
        </AuthContext.Provider>
      </ThemeContext.Provider>
    </MemoryRouter>,
  )
}

describe('topbar account menu', () => {
  it('offers a workspace help entry point', () => {
    renderTopbar(vi.fn().mockResolvedValue(undefined))

    expect(screen.getByRole('link', { name: 'Open help' })).toHaveAttribute(
      'href',
      '/workspaces/workspace-1/help',
    )
  })

  it('requires an explicit Sign out menu choice before ending the session', async () => {
    const logout = vi.fn().mockResolvedValue(undefined)
    const user = userEvent.setup()
    renderTopbar(logout)

    await user.click(screen.getByRole('button', { name: /Open account menu/ }))

    expect(logout).not.toHaveBeenCalled()
    await user.click(screen.getByRole('menuitem', { name: 'Sign out' }))
    expect(logout).toHaveBeenCalledOnce()
  })

  it('closes on Escape and returns focus to the account control', async () => {
    const user = userEvent.setup()
    renderTopbar(vi.fn().mockResolvedValue(undefined))
    const trigger = screen.getByRole('button', { name: /Open account menu/ })

    await user.click(trigger)
    await user.keyboard('{Escape}')

    expect(screen.queryByRole('menu', { name: 'Account' })).toBeNull()
    expect(trigger).toHaveFocus()
  })
})
