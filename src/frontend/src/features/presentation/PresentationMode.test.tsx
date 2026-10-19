import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { AuthContext, type AuthContextValue } from '@/features/auth/AuthContext'
import { PresentationPage } from '@/features/presentation/pages/PresentationPage'
import {
  ProjectContext,
  type ProjectContextValue,
} from '@/features/projects/ProjectContext'
import {
  WorkspaceContext,
  type WorkspaceContextValue,
} from '@/features/workspaces/WorkspaceContext'

const auth: AuthContextValue = {
  status: 'authenticated',
  user: {
    id: 'advisor-1',
    email: 'advisor@example.com',
    displayName: 'Advisor',
  },
  accessToken: 'advisor-token',
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}

const workspace: WorkspaceContextValue = {
  status: 'ready',
  workspaces: [
    {
      id: 'workspace-1',
      name: 'UIT Software Engineering',
      role: 'Advisor',
      memberCount: 4,
    },
  ],
  currentWorkspace: {
    id: 'workspace-1',
    name: 'UIT Software Engineering',
    role: 'Advisor',
    memberCount: 4,
  },
  error: null,
  createWorkspace: vi.fn(),
  selectWorkspace: vi.fn(),
  reload: vi.fn(),
}

const project: ProjectContextValue = {
  status: 'ready',
  projects: [],
  currentProject: {
    id: 'project-1',
    workspaceId: 'workspace-1',
    name: 'TeamPulse MVP',
    description:
      'Make accepted contribution evidence visible and presentation-ready.',
    isArchived: false,
    createdAtUtc: '2026-10-01T00:00:00Z',
    updatedAtUtc: '2026-10-18T00:00:00Z',
    archivedAtUtc: null,
  },
  error: null,
  createProject: vi.fn(),
  updateProject: vi.fn(),
  archiveProject: vi.fn(),
  reload: vi.fn(),
}

function response(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

function renderPresentation() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={auth}>
        <WorkspaceContext.Provider value={workspace}>
          <ProjectContext.Provider value={project}>
            <MemoryRouter
              initialEntries={[
                '/workspaces/workspace-1/projects/project-1/presentation',
              ]}
            >
              <Routes>
                <Route
                  path="/workspaces/:workspaceId/projects/:projectId/presentation"
                  element={<PresentationPage />}
                />
              </Routes>
            </MemoryRouter>
          </ProjectContext.Provider>
        </WorkspaceContext.Provider>
      </AuthContext.Provider>
    </QueryClientProvider>,
  )
}

afterEach(() => vi.restoreAllMocks())

describe('Presentation mode', () => {
  it('gives an advisor a read-only project story without the app sidebar', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)
      if (url.endsWith('/dashboard')) {
        return response({
          kpis: {
            totalTasks: 4,
            doneTasks: 2,
            openTasks: 2,
            inReviewTasks: 1,
            blockedTasks: 0,
            dueSoonTasks: 1,
            progressPercent: 50,
          },
          statusBreakdown: [
            { status: 'Done', count: 2 },
            { status: 'InReview', count: 1 },
            { status: 'InProgress', count: 1 },
          ],
          workload: [],
          myPriorityTasks: [],
          recentActivity: [],
          contributions: [
            {
              userId: 'member-1',
              displayName: 'Bao Trung',
              role: 'Member',
              acceptedTasks: 2,
              acceptedEvidence: 3,
              reviews: 1,
              lastContributedAtUtc: '2026-10-18T00:00:00Z',
            },
          ],
        })
      }
      return response([
        {
          id: 'resource-1',
          workspaceId: 'workspace-1',
          projectId: 'project-1',
          title: 'TeamPulse repository',
          type: 'GitHub',
          url: 'https://github.com/example/teampulse',
          externalId: null,
          owner: {
            userId: 'member-1',
            displayName: 'Bao Trung',
            email: 'member@example.com',
          },
          linkedTask: null,
          createdAtUtc: '2026-10-18T00:00:00Z',
          updatedAtUtc: '2026-10-18T00:00:00Z',
        },
      ])
    })

    renderPresentation()

    expect(
      await screen.findByRole('heading', { name: 'TeamPulse MVP' }),
    ).toBeVisible()
    expect(screen.getByText('Review and validation')).toBeVisible()
    expect(screen.getByText('Bao Trung')).toBeVisible()
    expect(
      screen.getByRole('link', { name: /TeamPulse repository/ }),
    ).toHaveAttribute('href', 'https://github.com/example/teampulse')
    expect(
      screen.queryByRole('navigation', { name: 'Primary navigation' }),
    ).toBeNull()
    expect(
      screen.queryByRole('button', { name: /create|edit|delete/i }),
    ).toBeNull()
  })
})
