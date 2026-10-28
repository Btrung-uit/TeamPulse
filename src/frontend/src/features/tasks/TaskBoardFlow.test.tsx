import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  MemoryRouter,
  Route,
  Routes,
  useLocation,
  useNavigate,
} from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ToastProvider } from '@/components/ui/Toast/ToastProvider'
import { AuthContext, type AuthContextValue } from '@/features/auth/AuthContext'
import {
  ProjectContext,
  type ProjectContextValue,
} from '@/features/projects/ProjectContext'
import type { ProjectSummary } from '@/features/projects/types'
import { TaskBoardPage } from '@/features/tasks/pages/TaskBoardPage'
import type { TaskDetails, TaskSummary } from '@/features/tasks/types'
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

const taskDetails: TaskDetails = {
  ...task,
  workspaceId: workspace.id,
  projectId: project.id,
  description: 'Ship a focused task board.',
  statusBeforeBlocked: null,
  archivedAtUtc: null,
  createdAtUtc: '2026-10-16T00:00:00Z',
  rowVersion: 'version-1',
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
  const navigate = useNavigate()
  return (
    <>
      <output data-testid="location">
        {location.pathname + location.search}
      </output>
      <button type="button" onClick={() => navigate(-1)}>
        Test browser back
      </button>
    </>
  )
}

function renderBoard(
  role: WorkspaceSummary['role'] = 'Leader',
  initialEntry = '/workspaces/workspace-1/projects/project-1/tasks',
  authValue: AuthContextValue = authContext,
) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <AuthContext.Provider value={authValue}>
          <WorkspaceContext.Provider value={workspaceContext(role)}>
            <ProjectContext.Provider value={projectContext}>
              <MemoryRouter initialEntries={[initialEntry]}>
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
  if (path.endsWith('/collaboration')) {
    return jsonResponse({ evidence: [], comments: [] })
  }
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

function stubBoardTasks(items: TaskSummary[]) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input)
    const optionResponse = optionsResponse(path)
    if (optionResponse) return optionResponse
    if (path.includes('/tasks?')) return jsonResponse(taskPage(items))
    throw new Error(`Unexpected request: ${path}`)
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('task board flow', () => {
  it('lets a leader move selected visible tasks to a sprint in one request', async () => {
    const secondTask = {
      ...task,
      id: 'task-2',
      code: 'TP-024',
      title: 'Build task detail',
      rowVersion: 'version-2',
    }
    let bulkRequest: unknown
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => {
        const path = String(input)
        const optionResponse = optionsResponse(path)
        if (optionResponse) return optionResponse
        if (path.endsWith('/tasks/sprint') && init?.method === 'PUT') {
          bulkRequest = JSON.parse(String(init.body))
          return new Response(null, { status: 204 })
        }
        if (path.includes('/tasks?')) {
          return jsonResponse(taskPage([task, secondTask]))
        }
        throw new Error(`Unexpected request: ${path}`)
      },
    )
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    renderBoard()

    await user.click(await screen.findByRole('checkbox', { name: /TP-023/ }))
    await user.click(screen.getByRole('checkbox', { name: /TP-024/ }))
    await user.selectOptions(screen.getByLabelText('Move selected tasks to'), 'sprint-1')
    await user.click(screen.getByRole('button', { name: 'Apply to 2 tasks' }))

    await waitFor(() => {
      expect(bulkRequest).toEqual({
        sprintId: 'sprint-1',
        tasks: [
          { taskId: 'task-1', rowVersion: '' },
          { taskId: 'task-2', rowVersion: 'version-2' },
        ],
      })
    })
    expect(await screen.findByText('2 tasks moved')).toBeInTheDocument()
    expect(screen.queryByLabelText('Bulk task actions')).not.toBeInTheDocument()
  })

  it('clears bulk selection when filters change and hides it from members', async () => {
    stubBoardTasks([task])
    const user = userEvent.setup()

    const leaderView = renderBoard()
    await user.click(await screen.findByRole('checkbox', { name: /TP-023/ }))
    expect(screen.getByLabelText('Bulk task actions')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Search tasks'), 'board')
    expect(screen.queryByLabelText('Bulk task actions')).not.toBeInTheDocument()

    leaderView.unmount()
    renderBoard('Member')
    await screen.findByText('Build task board')
    expect(screen.queryByRole('checkbox', { name: /TP-023/ })).not.toBeInTheDocument()
  })

  it('supports guarded task-board keyboard shortcuts', async () => {
    stubBoardTasks([task])
    const user = userEvent.setup()

    renderBoard()
    const search = await screen.findByLabelText('Search tasks')

    await user.click(await screen.findByRole('checkbox', { name: /TP-023/ }))
    expect(screen.getByLabelText('Bulk task actions')).toBeInTheDocument()
    await user.keyboard('{Escape}')
    expect(screen.queryByLabelText('Bulk task actions')).not.toBeInTheDocument()
    await user.keyboard('/')
    expect(search).toHaveFocus()
    await user.keyboard('n')
    expect(screen.queryByRole('dialog', { name: 'Create task' })).not.toBeInTheDocument()
    await user.tab()
    await user.keyboard('n')
    expect(screen.getByRole('dialog', { name: 'Create task' })).toBeInTheDocument()
  })

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

    await user.click(screen.getByRole('button', { name: 'Filters' }))
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

  it('discloses secondary filters only when requested or already active', async () => {
    stubBoardTasks([task])
    const user = userEvent.setup()

    const view = renderBoard()

    expect(await screen.findByText('Build task board')).toBeInTheDocument()
    expect(screen.queryByLabelText('Priority')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Filters' }))
    expect(screen.getByLabelText('Priority')).toBeInTheDocument()

    view.unmount()
    renderBoard(
      'Leader',
      '/workspaces/workspace-1/projects/project-1/tasks?priority=High',
    )
    expect(await screen.findByLabelText('Priority')).toHaveValue('High')
    expect(
      screen.getByRole('button', { name: 'Filters (1)' }),
    ).toBeInTheDocument()
  })

  it('reopens filters when browser history restores an active filter', async () => {
    stubBoardTasks([task])
    const user = userEvent.setup()

    renderBoard(
      'Leader',
      '/workspaces/workspace-1/projects/project-1/tasks?priority=High',
    )

    await user.click(screen.getByRole('button', { name: 'Clear' }))
    await user.click(screen.getByRole('button', { name: 'Filters' }))
    expect(screen.queryByLabelText('Priority')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Test browser back' }))

    expect(await screen.findByLabelText('Priority')).toHaveValue('High')
  })

  it('does not describe the dashboard open-tasks deep link as full workflow', async () => {
    stubBoardTasks([task])

    renderBoard(
      'Leader',
      '/workspaces/workspace-1/projects/project-1/tasks?view=open',
    )

    expect(
      await screen.findByRole('button', { name: 'Full workflow' }),
    ).toHaveAttribute('aria-pressed', 'false')
    expect(screen.getByText('Open tasks only')).toBeInTheDocument()
  })

  it('renders needs-attention work as a compact list instead of seven columns', async () => {
    const blockedTask = { ...task, status: 'Blocked' as const }
    const normalTask = {
      ...task,
      id: 'task-2',
      code: 'TP-024',
      title: 'Normal queued task',
      status: 'Todo' as const,
      dueOn: null,
    }
    stubBoardTasks([blockedTask, normalTask])

    renderBoard(
      'Leader',
      '/workspaces/workspace-1/projects/project-1/tasks?attention=true',
    )

    const attentionList = await screen.findByRole('region', {
      name: 'Tasks needing attention',
    })
    expect(
      within(attentionList).getByText('Build task board'),
    ).toBeInTheDocument()
    expect(
      within(attentionList).queryByText('Normal queued task'),
    ).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Kanban task board')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'My tasks' })).toHaveAttribute(
      'href',
      '/workspaces/workspace-1/projects/project-1/my-tasks',
    )
    expect(
      screen.getByRole('button', { name: 'Needs attention' }),
    ).toHaveAttribute('aria-pressed', 'true')
  })

  it('exports the current project filters and offers a leader template', async () => {
    const createObjectUrl = vi
      .spyOn(URL, 'createObjectURL')
      .mockReturnValue('blob:task-snapshot')
    const revokeObjectUrl = vi
      .spyOn(URL, 'revokeObjectURL')
      .mockImplementation(() => undefined)
    const anchorClick = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined)
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input)
      const optionResponse = optionsResponse(path)
      if (optionResponse) return optionResponse
      if (path.includes('/tasks/export?')) {
        return new Response(new Blob(['xlsx']), {
          headers: {
            'Content-Type':
              'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            'Content-Disposition':
              "attachment; filename*=UTF-8''teampulse-mvp-tasks.xlsx",
          },
        })
      }
      if (path.includes('/tasks?')) return jsonResponse(taskPage([task]))
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    renderBoard(
      'Leader',
      '/workspaces/workspace-1/projects/project-1/tasks?priority=High&view=open&attention=true&sort=code-asc',
    )

    expect(
      await screen.findByRole('button', { name: 'Download template' }),
    ).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Export snapshot' }))

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        expect.stringMatching(
          /tasks\/export\?.*priority=High.*view=open.*attention=true.*sort=code-asc/,
        ),
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: 'Bearer test-token',
          }),
        }),
      )
    })
    expect(createObjectUrl).toHaveBeenCalledWith(expect.any(Blob))
    expect(anchorClick).toHaveBeenCalledOnce()
    expect(revokeObjectUrl).toHaveBeenCalledWith('blob:task-snapshot')
    expect(await screen.findByText('Snapshot exported')).toBeInTheDocument()
  })

  it('shows a safe error when spreadsheet export fails', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input)
      const optionResponse = optionsResponse(path)
      if (optionResponse) return optionResponse
      if (path.includes('/tasks/export?')) {
        return jsonResponse(
          {
            code: 'spreadsheet_unavailable',
            message: 'Export is temporarily unavailable.',
          },
          503,
        )
      }
      if (path.includes('/tasks?')) return jsonResponse(taskPage([task]))
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    renderBoard()
    await user.click(
      await screen.findByRole('button', { name: 'Export snapshot' }),
    )

    expect(
      await screen.findByText('Spreadsheet download failed'),
    ).toBeInTheDocument()
    expect(
      await screen.findByText('Export is temporarily unavailable.'),
    ).toBeInTheDocument()
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

  it('rejects invalid task identity fields before calling the API', async () => {
    const fetchMock = vi.fn(
      async (...args: [RequestInfo | URL, RequestInit?]) => {
        const [input] = args
        const path = String(input)
        const optionResponse = optionsResponse(path)
        if (optionResponse) return optionResponse
        if (path.includes('/tasks?')) return jsonResponse(taskPage([]))
        throw new Error(`Unexpected request: ${path}`)
      },
    )
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    renderBoard()

    const newTaskButtons = await screen.findAllByRole('button', {
      name: 'New task',
    })
    await user.click(newTaskButtons[0])
    const dialog = screen.getByRole('dialog', { name: 'Create task' })
    await user.type(within(dialog).getByLabelText('Task code'), 'invalid')
    const submit = within(dialog).getByRole('button', { name: 'Create task' })
    await waitFor(() => expect(submit).toBeEnabled())
    await user.click(submit)

    expect(
      within(dialog).getByText('Use PREFIX-NUMBER, for example TP-123.'),
    ).toBeVisible()
    expect(
      within(dialog).getByText(
        'Enter a title with no more than 240 characters.',
      ),
    ).toBeVisible()
    expect(within(dialog).getByLabelText('Task code')).toHaveAttribute(
      'aria-invalid',
      'true',
    )
    expect(within(dialog).getByLabelText('Title')).toHaveAttribute(
      'aria-invalid',
      'true',
    )
    expect(
      fetchMock.mock.calls.some(
        ([path, init]) =>
          String(path).endsWith('/tasks') && init?.method === 'POST',
      ),
    ).toBe(false)
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
    expect(
      screen.queryByRole('button', { name: 'Download template' }),
    ).toBeNull()
    expect(screen.queryByRole('button', { name: 'Export snapshot' })).toBeNull()
  })

  it('opens task detail from a card and preserves board filters in the URL', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input)
      const optionResponse = optionsResponse(path)
      if (optionResponse) return optionResponse
      if (path.endsWith('/tasks/task-1')) return jsonResponse(taskDetails)
      if (path.endsWith('/dependencies')) return jsonResponse([])
      if (path.includes('/tasks?')) return jsonResponse(taskPage([task]))
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    renderBoard(
      'Leader',
      '/workspaces/workspace-1/projects/project-1/tasks?priority=High',
    )

    await user.click(
      await screen.findByRole('button', {
        name: 'Open TP-023: Build task board',
      }),
    )
    expect(
      await screen.findByRole('dialog', { name: 'TP-023 task details' }),
    ).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent(
      '?priority=High&task=task-1',
    )

    await user.click(screen.getByRole('button', { name: 'Close task detail' }))
    expect(
      screen.queryByRole('dialog', { name: 'TP-023 task details' }),
    ).toBeNull()
    expect(screen.getByTestId('location')).toHaveTextContent('?priority=High')
  })

  it('explains a stale update and retries explicitly with the latest row version', async () => {
    let getCount = 0
    const putBodies: unknown[] = []
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => {
        const path = String(input)
        const optionResponse = optionsResponse(path)
        if (optionResponse) return optionResponse
        if (path.endsWith('/dependencies')) return jsonResponse([])
        if (path.endsWith('/tasks/task-1') && init?.method === 'PUT') {
          putBodies.push(JSON.parse(String(init.body)))
          if (putBodies.length === 1) {
            return jsonResponse(
              { code: 'task_conflict', message: 'Reload and retry.' },
              409,
            )
          }
          return jsonResponse({
            ...taskDetails,
            title: 'Updated by leader',
            rowVersion: 'version-3',
          })
        }
        if (path.endsWith('/tasks/task-1')) {
          getCount += 1
          return jsonResponse({
            ...taskDetails,
            rowVersion: getCount > 1 ? 'version-2' : 'version-1',
          })
        }
        if (path.includes('/tasks?')) return jsonResponse(taskPage([task]))
        throw new Error(`Unexpected request: ${path}`)
      },
    )
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    renderBoard(
      'Leader',
      '/workspaces/workspace-1/projects/project-1/tasks?task=task-1',
    )
    const dialog = await screen.findByRole('dialog', {
      name: 'TP-023 task details',
    })
    expect(
      within(dialog).getByText(
        'Discard restores the last saved values. Save applies your edits to this task.',
      ),
    ).toBeVisible()
    const title = within(dialog).getByLabelText('Title')
    await user.clear(title)
    await user.type(title, 'Updated by leader')
    await user.click(
      within(dialog).getByRole('button', { name: 'Save changes' }),
    )

    expect(
      await within(dialog).findByText('This task changed after you opened it.'),
    ).toBeInTheDocument()
    expect(title).toHaveValue('Updated by leader')
    await user.click(
      within(dialog).getByRole('button', { name: 'Retry my action' }),
    )

    await waitFor(() => expect(putBodies).toHaveLength(2))
    expect(putBodies).toEqual([
      expect.objectContaining({
        title: 'Updated by leader',
        rowVersion: 'version-1',
      }),
      expect.objectContaining({
        title: 'Updated by leader',
        rowVersion: 'version-2',
      }),
    ])
    expect(
      await screen.findByText('Task updated with the latest version'),
    ).toBeInTheDocument()
  })

  it('requires confirmation before changing a task deadline', async () => {
    const putBodies: unknown[] = []
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => {
        const path = String(input)
        const optionResponse = optionsResponse(path)
        if (optionResponse) return optionResponse
        if (path.endsWith('/dependencies')) return jsonResponse([])
        if (path.endsWith('/tasks/task-1') && init?.method === 'PUT') {
          const body = JSON.parse(String(init.body))
          putBodies.push(body)
          return jsonResponse({
            ...taskDetails,
            dueOn: body.dueOn,
            rowVersion: 'version-2',
          })
        }
        if (path.endsWith('/tasks/task-1')) return jsonResponse(taskDetails)
        if (path.includes('/tasks?')) return jsonResponse(taskPage([task]))
        throw new Error(`Unexpected request: ${path}`)
      },
    )
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    renderBoard(
      'Leader',
      '/workspaces/workspace-1/projects/project-1/tasks?task=task-1',
    )
    const detailsDialog = await screen.findByRole('dialog', {
      name: 'TP-023 task details',
    })
    await user.clear(within(detailsDialog).getByLabelText('Deadline'))
    await user.type(
      within(detailsDialog).getByLabelText('Deadline'),
      '2026-10-25',
    )
    await user.click(
      within(detailsDialog).getByRole('button', { name: 'Save changes' }),
    )

    const confirmDialog = screen.getByRole('dialog', {
      name: 'Confirm deadline change',
    })
    expect(putBodies).toHaveLength(0)
    expect(within(confirmDialog).getByText('2026-10-20')).toBeVisible()
    expect(within(confirmDialog).getByText('2026-10-25')).toBeVisible()
    await user.click(
      within(confirmDialog).getByRole('button', { name: 'Update deadline' }),
    )

    await waitFor(() =>
      expect(putBodies).toEqual([
        expect.objectContaining({
          dueOn: '2026-10-25',
          rowVersion: 'version-1',
        }),
      ]),
    )
  })

  it('requires confirmation before removing a dependency', async () => {
    let removed = false
    const edge = {
      predecessor: {
        id: 'task-0',
        code: 'TP-022',
        title: 'Build task API',
        status: 'Done',
        dueOn: '2026-10-18',
        isArchived: false,
      },
      successor: {
        id: task.id,
        code: task.code,
        title: task.title,
        status: task.status,
        dueOn: task.dueOn,
        isArchived: false,
      },
      isAtRisk: false,
      risks: [],
      createdAtUtc: '2026-10-17T00:00:00Z',
    }
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => {
        const path = String(input)
        const optionResponse = optionsResponse(path)
        if (optionResponse) return optionResponse
        if (
          path.endsWith('/dependencies/task-0/task-1') &&
          init?.method === 'DELETE'
        ) {
          removed = true
          return new Response(null, { status: 204 })
        }
        if (path.endsWith('/dependencies')) {
          return jsonResponse(removed ? [] : [edge])
        }
        if (path.endsWith('/tasks/task-1')) return jsonResponse(taskDetails)
        if (path.includes('/tasks?')) return jsonResponse(taskPage([task]))
        throw new Error(`Unexpected request: ${path}`)
      },
    )
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    renderBoard(
      'Leader',
      '/workspaces/workspace-1/projects/project-1/tasks?task=task-1',
    )
    const detailsDialog = await screen.findByRole('dialog', {
      name: 'TP-023 task details',
    })
    await user.click(
      await within(detailsDialog).findByRole('button', {
        name: 'Remove dependency on TP-022',
      }),
    )

    const confirmDialog = screen.getByRole('dialog', {
      name: 'Remove this dependency?',
    })
    expect(removed).toBe(false)
    await user.click(
      within(confirmDialog).getByRole('button', {
        name: 'Remove dependency',
      }),
    )
    await waitFor(() => expect(removed).toBe(true))
    expect(await screen.findByText('Dependency removed')).toBeVisible()
  })

  it('keeps content read-only for a member while allowing an assignee status transition', async () => {
    const memberAuth: AuthContextValue = {
      ...authContext,
      user: {
        id: 'member-1',
        email: 'member@example.com',
        displayName: 'Bao Trung',
      },
    }
    const patchBodies: unknown[] = []
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => {
        const path = String(input)
        const optionResponse = optionsResponse(path)
        if (optionResponse) return optionResponse
        if (path.endsWith('/dependencies')) return jsonResponse([])
        if (path.endsWith('/tasks/task-1/status') && init?.method === 'PATCH') {
          patchBodies.push(JSON.parse(String(init.body)))
          return jsonResponse({
            ...taskDetails,
            status: 'EvidenceLinked',
            rowVersion: 'version-2',
          })
        }
        if (path.endsWith('/tasks/task-1')) return jsonResponse(taskDetails)
        if (path.includes('/tasks?')) return jsonResponse(taskPage([task]))
        throw new Error(`Unexpected request: ${path}`)
      },
    )
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    renderBoard(
      'Member',
      '/workspaces/workspace-1/projects/project-1/tasks?task=task-1',
      memberAuth,
    )
    const dialog = await screen.findByRole('dialog', {
      name: 'TP-023 task details',
    })
    expect(within(dialog).getByLabelText('Title')).toHaveAttribute('readonly')
    expect(
      within(dialog).queryByRole('button', { name: 'Save changes' }),
    ).toBeNull()
    expect(within(dialog).queryByRole('button', { name: 'Archive' })).toBeNull()

    await user.click(
      within(dialog).getByRole('button', { name: 'Evidence linked' }),
    )
    await waitFor(() =>
      expect(patchBodies).toEqual([
        { status: 'EvidenceLinked', rowVersion: 'version-1' },
      ]),
    )
  })

  it('links evidence and lets a leader record an explicit review decision', async () => {
    let evidenceLinked = false
    let reviewAccepted = false
    const evidenceBodies: unknown[] = []
    const reviewBodies: unknown[] = []
    const reviewTask = { ...task, status: 'InReview' as const }
    const reviewDetails = { ...taskDetails, status: 'InReview' as const }
    const evidence = {
      id: 'evidence-1',
      type: 'PullRequest',
      url: 'https://github.com/example/teampulse/pull/42',
      note: 'Ready for review',
      submittedBy: authContext.user,
      createdAtUtc: '2026-10-17T01:00:00Z',
    }
    const comment = {
      id: 'comment-1',
      kind: 'ReviewAccepted',
      body: 'Verified and accepted.',
      author: authContext.user,
      createdAtUtc: '2026-10-17T02:00:00Z',
    }
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => {
        const path = String(input)
        if (path.endsWith('/tasks/task-1/collaboration')) {
          return jsonResponse({
            evidence: evidenceLinked ? [evidence] : [],
            comments: reviewAccepted ? [comment] : [],
          })
        }
        if (
          path.endsWith('/tasks/task-1/evidence') &&
          init?.method === 'POST'
        ) {
          evidenceBodies.push(JSON.parse(String(init.body)))
          evidenceLinked = true
          return jsonResponse(evidence, 201)
        }
        if (path.endsWith('/tasks/task-1/review') && init?.method === 'POST') {
          reviewBodies.push(JSON.parse(String(init.body)))
          reviewAccepted = true
          return jsonResponse({
            taskId: task.id,
            status: 'Done',
            rowVersion: 'version-2',
            comment,
          })
        }
        const optionResponse = optionsResponse(path)
        if (optionResponse) return optionResponse
        if (path.endsWith('/dependencies')) return jsonResponse([])
        if (path.endsWith('/tasks/task-1')) {
          return jsonResponse(
            reviewAccepted
              ? { ...reviewDetails, status: 'Done', rowVersion: 'version-2' }
              : reviewDetails,
          )
        }
        if (path.includes('/tasks?')) {
          return jsonResponse(
            taskPage(
              reviewAccepted
                ? [{ ...reviewTask, status: 'Done' }]
                : [reviewTask],
            ),
          )
        }
        throw new Error(`Unexpected request: ${path}`)
      },
    )
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    renderBoard()
    await user.click(
      await screen.findByRole('button', {
        name: 'Open TP-023: Build task board',
      }),
    )
    const dialog = await screen.findByRole('dialog', {
      name: 'TP-023 task details',
    })
    await user.selectOptions(
      within(dialog).getByLabelText('Evidence type'),
      'PullRequest',
    )
    await user.type(within(dialog).getByLabelText('Evidence URL'), evidence.url)
    await user.type(
      within(dialog).getByLabelText('Evidence note'),
      evidence.note,
    )
    await user.click(
      within(dialog).getByRole('button', { name: 'Link evidence' }),
    )

    await waitFor(() =>
      expect(evidenceBodies).toEqual([
        {
          type: 'PullRequest',
          url: evidence.url,
          note: evidence.note,
        },
      ]),
    )
    expect(
      await within(dialog).findByRole('link', { name: 'Open evidence' }),
    ).toHaveAttribute('href', evidence.url)

    await user.type(
      within(dialog).getByLabelText('Review comment'),
      comment.body,
    )
    await user.click(
      within(dialog).getByRole('button', { name: 'Accept task' }),
    )
    await waitFor(() =>
      expect(reviewBodies).toEqual([
        {
          decision: 'Accept',
          comment: comment.body,
          rowVersion: 'version-1',
        },
      ]),
    )
    expect(await screen.findByText('Task accepted')).toBeInTheDocument()
  })

  it('requires archive confirmation before removing the task from the active board', async () => {
    let archived = false
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => {
        const path = String(input)
        const optionResponse = optionsResponse(path)
        if (optionResponse) return optionResponse
        if (path.endsWith('/dependencies')) return jsonResponse([])
        if (path.endsWith('/tasks/task-1/archive') && init?.method === 'POST') {
          archived = true
          return jsonResponse({
            ...taskDetails,
            isArchived: true,
            archivedAtUtc: '2026-10-17T01:00:00Z',
          })
        }
        if (path.endsWith('/tasks/task-1')) return jsonResponse(taskDetails)
        if (path.includes('/tasks?')) {
          return jsonResponse(taskPage(archived ? [] : [task]))
        }
        throw new Error(`Unexpected request: ${path}`)
      },
    )
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    renderBoard(
      'Leader',
      '/workspaces/workspace-1/projects/project-1/tasks?task=task-1',
    )
    const detailDialog = await screen.findByRole('dialog', {
      name: 'TP-023 task details',
    })
    await user.click(
      within(detailDialog).getByRole('button', { name: 'Archive' }),
    )
    expect(archived).toBe(false)

    const confirmDialog = screen.getByRole('dialog', {
      name: 'Archive this task?',
    })
    await user.click(
      within(confirmDialog).getByRole('button', { name: 'Archive task' }),
    )

    await waitFor(() => expect(archived).toBe(true))
    await waitFor(() =>
      expect(
        screen.queryByRole('dialog', { name: 'TP-023 task details' }),
      ).toBeNull(),
    )
    expect(screen.getByTestId('location')).not.toHaveTextContent('task=task-1')
  })
})
