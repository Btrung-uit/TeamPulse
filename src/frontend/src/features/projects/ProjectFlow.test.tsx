import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ToastProvider } from '@/components/ui/Toast/ToastProvider'
import { AuthContext, type AuthContextValue } from '@/features/auth/AuthContext'
import {
  ProjectContext,
  type ProjectContextValue,
} from '@/features/projects/ProjectContext'
import { ProjectProvider } from '@/features/projects/ProjectProvider'
import { ProjectListPage } from '@/features/projects/pages/ProjectListPage'
import type { ProjectSummary } from '@/features/projects/types'
import type { WorkspaceSummary } from '@/features/workspaces/types'
import {
  WorkspaceContext,
  type WorkspaceContextValue,
} from '@/features/workspaces/WorkspaceContext'

const workspace: WorkspaceSummary = {
  id: 'workspace-1',
  name: 'UIT Engineering',
  role: 'Leader',
  memberCount: 4,
}

const project: ProjectSummary = {
  id: 'project-1',
  workspaceId: workspace.id,
  name: 'TeamPulse MVP',
  description: 'Collaboration workflow',
  isArchived: false,
  createdAtUtc: '2026-10-16T00:00:00Z',
  updatedAtUtc: '2026-10-16T00:00:00Z',
  archivedAtUtc: null,
}

const authContext: AuthContextValue = {
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

const workspaceContext: WorkspaceContextValue = {
  status: 'ready',
  workspaces: [workspace],
  currentWorkspace: workspace,
  error: null,
  createWorkspace: vi.fn(),
  selectWorkspace: vi.fn(),
  reload: vi.fn(),
}

function projectContext(
  overrides: Partial<ProjectContextValue> = {},
): ProjectContextValue {
  return {
    status: 'ready',
    projects: [],
    currentProject: null,
    error: null,
    createProject: vi.fn(),
    updateProject: vi.fn(),
    archiveProject: vi.fn(),
    reload: vi.fn(),
    ...overrides,
  }
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('project flow', () => {
  it('restores project context from a deep link after loading the workspace projects', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify([project]), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    function ProjectName() {
      return (
        <ProjectContext.Consumer>
          {(context) => (
            <div>{context?.currentProject?.name ?? 'No project'}</div>
          )}
        </ProjectContext.Consumer>
      )
    }

    render(
      <AuthContext.Provider value={authContext}>
        <MemoryRouter
          initialEntries={['/workspaces/workspace-1/projects/project-1/tasks']}
        >
          <Routes>
            <Route
              path="/workspaces/:workspaceId/projects/:projectId/tasks"
              element={
                <ProjectProvider>
                  <ProjectName />
                </ProjectProvider>
              }
            />
          </Routes>
        </MemoryRouter>
      </AuthContext.Provider>,
    )

    expect(await screen.findByText('TeamPulse MVP')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/workspaces/workspace-1/projects?includeArchived=true',
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
      }),
    )
  })

  it('lets a leader create the first project from the empty state', async () => {
    const user = userEvent.setup()
    const createProject = vi.fn().mockResolvedValue(project)

    render(
      <ToastProvider>
        <WorkspaceContext.Provider value={workspaceContext}>
          <ProjectContext.Provider value={projectContext({ createProject })}>
            <MemoryRouter initialEntries={['/workspaces/workspace-1/projects']}>
              <Routes>
                <Route
                  path="/workspaces/:workspaceId/projects"
                  element={<ProjectListPage />}
                />
                <Route
                  path="/workspaces/:workspaceId/projects/:projectId"
                  element={<div>Project dashboard</div>}
                />
              </Routes>
            </MemoryRouter>
          </ProjectContext.Provider>
        </WorkspaceContext.Provider>
      </ToastProvider>,
    )

    await user.click(
      screen.getByRole('button', { name: 'Create first project' }),
    )
    await user.type(screen.getByLabelText('Project name'), '  TeamPulse MVP  ')
    await user.type(
      screen.getByLabelText('Description'),
      '  Collaboration workflow  ',
    )
    await user.click(screen.getByRole('button', { name: 'Create project' }))

    await waitFor(() => {
      expect(createProject).toHaveBeenCalledWith({
        name: 'TeamPulse MVP',
        description: 'Collaboration workflow',
      })
    })
    expect(await screen.findByText('Project dashboard')).toBeInTheDocument()
  })
})
