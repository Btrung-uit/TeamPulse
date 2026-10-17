import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { MobileNavigation } from '@/app/components/MobileNavigation'
import { ToastProvider } from '@/components/ui/Toast/ToastProvider'
import { AuthContext, type AuthContextValue } from '@/features/auth/AuthContext'
import {
  ProjectContext,
  type ProjectContextValue,
} from '@/features/projects/ProjectContext'
import type { ProjectSummary } from '@/features/projects/types'
import { MyTasksPage } from '@/features/tasks/pages/MyTasksPage'
import type { TaskDetails, TaskSummary } from '@/features/tasks/types'
import type { WorkspaceSummary } from '@/features/workspaces/types'
import {
  WorkspaceContext,
  type WorkspaceContextValue,
} from '@/features/workspaces/WorkspaceContext'

const workspace: WorkspaceSummary = {
  id: 'workspace-1',
  name: 'UIT Engineering',
  role: 'Member',
  memberCount: 3,
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
    id: 'member-1',
    email: 'member@example.com',
    displayName: 'Bao Trung',
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

const projectContext: ProjectContextValue = {
  status: 'ready',
  projects: [project],
  currentProject: project,
  error: null,
  createProject: vi.fn(),
  updateProject: vi.fn(),
  archiveProject: vi.fn(),
  reload: vi.fn(),
}

function task(overrides: Partial<TaskSummary>): TaskSummary {
  return {
    id: 'task-normal',
    code: 'TP-025',
    title: 'Build mobile monitoring',
    status: 'Todo',
    priority: 'Medium',
    dueOn: null,
    estimateHours: 4,
    assignee: {
      userId: 'member-1',
      displayName: 'Bao Trung',
      email: 'member@example.com',
    },
    reviewer: null,
    sprint: null,
    isArchived: false,
    updatedAtUtc: '2026-10-17T00:00:00Z',
    rowVersion: 'version-1',
    ...overrides,
  }
}

const tasks = [
  task({
    id: 'task-blocked',
    code: 'TP-001',
    title: 'Blocked task',
    status: 'Blocked',
  }),
  task({
    id: 'task-overdue',
    code: 'TP-002',
    title: 'Overdue task',
    status: 'InProgress',
    dueOn: '2020-01-01',
  }),
  task({ id: 'task-normal', code: 'TP-003', title: 'Normal task' }),
  task({
    id: 'task-done',
    code: 'TP-004',
    title: 'Completed task',
    status: 'Done',
  }),
]

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function taskPage(items: TaskSummary[]) {
  return {
    items,
    page: 1,
    pageSize: 100,
    totalCount: items.length,
    totalPages: 1,
  }
}

function LocationProbe() {
  const location = useLocation()
  return (
    <output data-testid="location">
      {location.pathname + location.search}
    </output>
  )
}

function Providers({ children }: { children: React.ReactNode }) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  })

  return (
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <AuthContext.Provider value={authContext}>
          <WorkspaceContext.Provider value={workspaceContext}>
            <ProjectContext.Provider value={projectContext}>
              {children}
            </ProjectContext.Provider>
          </WorkspaceContext.Provider>
        </AuthContext.Provider>
      </ToastProvider>
    </QueryClientProvider>
  )
}

function renderMyTasks(
  initialEntry = '/workspaces/workspace-1/projects/project-1/my-tasks',
) {
  return render(
    <Providers>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route
            path="/workspaces/:workspaceId/projects/:projectId/my-tasks"
            element={
              <>
                <MyTasksPage />
                <LocationProbe />
              </>
            }
          />
        </Routes>
      </MemoryRouter>
    </Providers>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('my tasks flow', () => {
  it('loads only the current assignee and prioritizes blocked and overdue work', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input)
      if (path.includes('/tasks?')) return jsonResponse(taskPage(tasks))
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)

    renderMyTasks()

    expect(await screen.findByText('Blocked task')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: 'Overdue task' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: 'Normal task' }),
    ).toBeInTheDocument()
    expect(screen.queryByText('Completed task')).toBeNull()
    expect(screen.getByText('Needs attention')).toBeInTheDocument()
    expect(screen.getByText('Next up')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('assigneeUserId=member-1'),
      expect.any(Object),
    )
    expect(
      screen
        .getAllByRole('button', { name: /^Open TP-/ })
        .map((button) => button.getAttribute('aria-label')),
    ).toEqual([
      'Open TP-001: Blocked task',
      'Open TP-002: Overdue task',
      'Open TP-003: Normal task',
    ])
  })

  it('keeps status and deadline filters in the project URL', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => jsonResponse(taskPage(tasks))),
    )
    const user = userEvent.setup()
    renderMyTasks()
    await screen.findByText('Blocked task')

    await user.selectOptions(screen.getByLabelText('Status'), 'Done')
    expect(await screen.findByText('Completed task')).toBeInTheDocument()
    expect(screen.queryByText('Blocked task')).toBeNull()
    expect(screen.getByTestId('location')).toHaveTextContent('?status=Done')

    await user.selectOptions(screen.getByLabelText('Deadline'), 'overdue')
    expect(screen.getByTestId('location')).toHaveTextContent(
      'status=Done&due=overdue',
    )
    expect(
      await screen.findByText('No tasks match these filters'),
    ).toBeInTheDocument()
  })

  it('opens full task detail and lets an assignee perform a permitted status change', async () => {
    const statusBodies: unknown[] = []
    const overdue = tasks[1]
    const details: TaskDetails = {
      ...overdue,
      workspaceId: workspace.id,
      projectId: project.id,
      description: 'Finish the monitoring slice.',
      statusBeforeBlocked: null,
      archivedAtUtc: null,
      createdAtUtc: '2026-10-16T00:00:00Z',
    }
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => {
        const path = String(input)
        if (path.endsWith('/members')) return jsonResponse([])
        if (path.endsWith('/sprints')) return jsonResponse([])
        if (path.endsWith('/dependencies')) return jsonResponse([])
        if (
          path.endsWith('/tasks/task-overdue/status') &&
          init?.method === 'PATCH'
        ) {
          statusBodies.push(JSON.parse(String(init.body)))
          return jsonResponse({
            ...details,
            status: 'EvidenceLinked',
            rowVersion: 'version-2',
          })
        }
        if (path.endsWith('/tasks/task-overdue')) return jsonResponse(details)
        if (path.includes('/tasks?')) return jsonResponse(taskPage(tasks))
        throw new Error(`Unexpected request: ${path}`)
      },
    )
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    renderMyTasks()

    await user.click(
      await screen.findByRole('button', { name: 'Open TP-002: Overdue task' }),
    )
    const dialog = await screen.findByRole('dialog', {
      name: 'TP-002 task details',
    })
    expect(within(dialog).getByLabelText('Title')).toHaveAttribute('readonly')
    expect(within(dialog).queryByRole('button', { name: 'Archive' })).toBeNull()
    await user.click(
      within(dialog).getByRole('button', { name: 'Evidence linked' }),
    )

    await waitFor(() =>
      expect(statusBodies).toEqual([
        { status: 'EvidenceLinked', rowVersion: 'version-1' },
      ]),
    )
    expect(screen.getByTestId('location')).toHaveTextContent(
      '?task=task-overdue',
    )
  })
})

describe('mobile monitoring navigation', () => {
  it('contains only the four low-risk destinations', () => {
    render(
      <Providers>
        <MemoryRouter
          initialEntries={[
            '/workspaces/workspace-1/projects/project-1/my-tasks',
          ]}
        >
          <MobileNavigation />
        </MemoryRouter>
      </Providers>,
    )

    const navigation = screen.getByRole('navigation', {
      name: 'Mobile navigation',
    })
    expect(
      within(navigation)
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual(['Home', 'Tasks', 'Inbox', 'More'])
    expect(within(navigation).queryByText(/Import|Admin/i)).toBeNull()
  })
})
