import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ToastProvider } from '@/components/ui/Toast/ToastProvider'
import { AuthContext, type AuthContextValue } from '@/features/auth/AuthContext'
import {
  ProjectContext,
  type ProjectContextValue,
} from '@/features/projects/ProjectContext'
import type { ProjectSummary } from '@/features/projects/types'
import { TaskBoardPage } from '@/features/tasks/pages/TaskBoardPage'
import type { TaskSummary } from '@/features/tasks/types'
import type { WorkspaceSummary } from '@/features/workspaces/types'
import {
  WorkspaceContext,
  type WorkspaceContextValue,
} from '@/features/workspaces/WorkspaceContext'

const workspace: WorkspaceSummary = {
  id: 'workspace-1',
  name: 'UIT Engineering',
  role: 'Leader',
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
    id: 'leader-1',
    email: 'leader@example.com',
    displayName: 'Team Leader',
  },
  accessToken: 'test-token',
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}

const task: TaskSummary = {
  id: 'task-1',
  code: 'TP-023',
  title: 'Build task board',
  status: 'InProgress',
  priority: 'High',
  dueOn: '2026-10-20',
  estimateHours: 8,
  assignee: {
    userId: 'member-1',
    displayName: 'Bao Trung',
    email: 'member@example.com',
  },
  reviewer: null,
  sprint: { id: 'sprint-1', name: 'Sprint 1' },
  isArchived: false,
  updatedAtUtc: '2026-10-17T00:00:00Z',
  rowVersion: '',
}

function workspaceContext(role: WorkspaceSummary['role'] = 'Leader') {
  const currentWorkspace = { ...workspace, role }
  return {
    status: 'ready',
    workspaces: [currentWorkspace],
    currentWorkspace,
    error: null,
    createWorkspace: vi.fn(),
    selectWorkspace: vi.fn(),
    reload: vi.fn(),
  } satisfies WorkspaceContextValue
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

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function taskPage(items: TaskSummary[], page = 1, totalPages = 1) {
  return {
    items,
    page,
    pageSize: 100,
    totalCount: items.length,
    totalPages,
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

function renderBoard(role: WorkspaceSummary['role'] = 'Leader') {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <AuthContext.Provider value={authContext}>
          <WorkspaceContext.Provider value={workspaceContext(role)}>
            <ProjectContext.Provider value={projectContext}>
              <MemoryRouter
                initialEntries={[
                  '/workspaces/workspace-1/projects/project-1/tasks',
                ]}
              >
                <Routes>
                  <Route
                    path="/workspaces/:workspaceId/projects/:projectId/tasks"
                    element={
                      <>
                        <TaskBoardPage />
                        <LocationProbe />
                      </>
                    }
                  />
                </Routes>
              </MemoryRouter>
            </ProjectContext.Provider>
          </WorkspaceContext.Provider>
        </AuthContext.Provider>
      </ToastProvider>
    </QueryClientProvider>,
  )
}

function optionsResponse(path: string) {
  if (path.endsWith('/members')) {
    return jsonResponse([
      {
        userId: 'member-1',
        email: 'member@example.com',
        displayName: 'Bao Trung',
        role: 'Member',
      },
    ])
  }
  if (path.endsWith('/sprints')) {
    return jsonResponse([
      {
        id: 'sprint-1',
        workspaceId: workspace.id,
        projectId: project.id,
        name: 'Sprint 1',
        goal: null,
        startsOn: '2026-10-17',
        endsOn: '2026-10-31',
        taskCount: 1,
        createdAtUtc: '2026-10-17T00:00:00Z',
        updatedAtUtc: '2026-10-17T00:00:00Z',
      },
    ])
  }
  return null
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('task board flow', () => {
  it('loads every API page and keeps filters in the current project URL', async () => {
    const secondTask = {
      ...task,
      id: 'task-2',
      code: 'TP-024',
      title: 'Build task detail',
      status: 'Todo' as const,
    }
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input)
      const optionResponse = optionsResponse(path)
      if (optionResponse) return optionResponse
      if (path.includes('/tasks?') && path.includes('page=2')) {
        return jsonResponse(taskPage([secondTask], 2, 2))
      }
      if (path.includes('/tasks?')) return jsonResponse(taskPage([task], 1, 2))
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    renderBoard()

    expect(await screen.findByText('Build task board')).toBeInTheDocument()
    expect(await screen.findByText('Build task detail')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('page=2&pageSize=100'),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
      }),
    )

    await user.selectOptions(screen.getByLabelText('Priority'), 'High')

    await waitFor(() => {
      expect(screen.getByTestId('location')).toHaveTextContent(
        '/workspaces/workspace-1/projects/project-1/tasks?priority=High',
      )
    })
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        expect.stringContaining('priority=High'),
        expect.any(Object),
      )
    })
  })

  it('lets a leader create a task and refreshes the board', async () => {
    let wasCreated = false
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => {
        const path = String(input)
        const optionResponse = optionsResponse(path)
        if (optionResponse) return optionResponse
        if (path.endsWith('/tasks') && init?.method === 'POST') {
          wasCreated = true
          return jsonResponse(task, 201)
        }
        if (path.includes('/tasks?')) {
          return jsonResponse(taskPage(wasCreated ? [task] : []))
        }
        throw new Error(`Unexpected request: ${path}`)
      },
    )
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    renderBoard()

    expect(
      await screen.findByRole('link', { name: /Import Excel/ }),
    ).toHaveAttribute(
      'href',
      '/workspaces/workspace-1/projects/project-1/import',
    )
    const newTaskButtons = await screen.findAllByRole('button', {
      name: 'New task',
    })
    await user.click(newTaskButtons[0])
    const dialog = screen.getByRole('dialog', { name: 'Create task' })
    await user.type(within(dialog).getByLabelText('Task code'), 'tp-023')
    await user.type(within(dialog).getByLabelText('Title'), 'Build task board')
    await user.selectOptions(
      within(dialog).getByLabelText('Assignee'),
      'member-1',
    )
    await user.selectOptions(
      within(dialog).getByLabelText('Sprint'),
      'sprint-1',
    )
    await user.selectOptions(within(dialog).getByLabelText('Priority'), 'High')
    await user.type(within(dialog).getByLabelText('Estimate hours'), '8')
    expect(within(dialog).getByLabelText('Task code')).toHaveValue('tp-023')
    expect(within(dialog).getByLabelText('Title')).toHaveValue(
      'Build task board',
    )
    expect(within(dialog).getByLabelText('Assignee')).toHaveValue('member-1')
    expect(within(dialog).getByLabelText('Sprint')).toHaveValue('sprint-1')
    expect(within(dialog).getByLabelText('Priority')).toHaveValue('High')
    expect(within(dialog).getByLabelText('Estimate hours')).toHaveValue(8)
    const submit = within(dialog).getByRole('button', { name: 'Create task' })
    await waitFor(() => expect(submit).toBeEnabled())
    expect(submit).toHaveAttribute('type', 'submit')
    expect(submit).toHaveAttribute('form', 'create-task-form')
    expect(document.querySelector('#create-task-form')).toBeValid()
    await user.click(submit)

    await waitFor(() => {
      const postCall = fetchMock.mock.calls.find(
        ([path, init]) =>
          String(path).endsWith('/tasks') && init?.method === 'POST',
      )
      expect(postCall).toBeDefined()
      expect(JSON.parse(String(postCall?.[1]?.body))).toEqual({
        code: 'TP-023',
        title: 'Build task board',
        assigneeUserId: 'member-1',
        sprintId: 'sprint-1',
        priority: 'High',
        estimateHours: 8,
        rowVersion: null,
      })
    })
    expect(await screen.findByText('Task created')).toBeInTheDocument()
    expect(await screen.findByText('Build task board')).toBeInTheDocument()
  })

  it('keeps creation controls hidden for members', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input)
      const optionResponse = optionsResponse(path)
      if (optionResponse) return optionResponse
      if (path.includes('/tasks?')) return jsonResponse(taskPage([]))
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)

    renderBoard('Member')

    expect(await screen.findByText('No tasks yet')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'New task' }),
    ).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Import Excel/ })).toBeNull()
  })
})
