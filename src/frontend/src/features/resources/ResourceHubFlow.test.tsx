import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ToastProvider } from '@/components/ui/Toast/ToastProvider'
import { ResourceHubPage } from '@/features/resources/pages/ResourceHubPage'

let workspaceRole: 'Leader' | 'Member' = 'Leader'

vi.mock('@/features/auth/useAuth', () => ({
  useAuth: () => ({ accessToken: 'test-token' }),
}))
vi.mock('@/features/workspaces/useWorkspace', () => ({
  useWorkspace: () => ({
    currentWorkspace: { id: 'workspace-1', role: workspaceRole },
  }),
}))
vi.mock('@/features/projects/useProject', () => ({
  useProject: () => ({
    currentProject: { id: 'project-1', name: 'TeamPulse MVP' },
  }),
}))

const resource = {
  id: 'resource-1',
  workspaceId: 'workspace-1',
  projectId: 'project-1',
  title: 'Architecture notes',
  type: 'GoogleDocs',
  url: 'https://docs.google.com/document/d/doc-1',
  externalId: 'doc-1',
  owner: {
    userId: 'member-1',
    displayName: 'Bao Trung',
    email: 'member@example.com',
  },
  linkedTask: { id: 'task-1', code: 'TP-032', title: 'Resource Hub' },
  createdAtUtc: '2026-10-18T00:00:00Z',
  updatedAtUtc: '2026-10-18T00:00:00Z',
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function mockApi() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
    const path = String(input)
    if (path.endsWith('/members')) {
      return Promise.resolve(
        jsonResponse([{ ...resource.owner, role: 'Member' }]),
      )
    }
    if (path.endsWith('/sprints')) return Promise.resolve(jsonResponse([]))
    if (path.includes('/tasks?')) {
      return Promise.resolve(
        jsonResponse({
          items: [
            {
              id: 'task-1',
              code: 'TP-032',
              title: 'Resource Hub',
              status: 'InProgress',
              priority: 'High',
              dueOn: null,
              estimateHours: null,
              assignee: null,
              reviewer: null,
              sprint: null,
              isArchived: false,
              updatedAtUtc: '2026-10-18T00:00:00Z',
              rowVersion: '',
            },
          ],
          page: 1,
          pageSize: 100,
          totalCount: 1,
          totalPages: 1,
        }),
      )
    }
    if (path.includes('/resources') && init?.method === 'POST') {
      const body = JSON.parse(String(init.body)) as Record<string, unknown>
      return Promise.resolve(
        jsonResponse({ ...resource, ...body, id: 'resource-2' }, 201),
      )
    }
    if (path.includes('/resources'))
      return Promise.resolve(jsonResponse([resource]))
    throw new Error(`Unexpected request: ${path}`)
  })
}

function renderHub() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <MemoryRouter
          initialEntries={[
            '/workspaces/workspace-1/projects/project-1/resources',
          ]}
        >
          <ResourceHubPage />
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}

describe('Resource Hub flow', () => {
  afterEach(() => {
    workspaceRole = 'Leader'
    vi.restoreAllMocks()
  })

  it('searches, previews and creates metadata without replacing the external editor', async () => {
    const user = userEvent.setup()
    const fetchMock = mockApi()
    renderHub()

    expect(
      (await screen.findAllByText('Architecture notes')).length,
    ).toBeGreaterThan(0)
    await user.type(screen.getByLabelText('Search resources'), 'architecture')
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([url]) =>
          String(url).includes('search=architecture'),
        ),
      ).toBe(true),
    )

    await user.click(
      screen.getAllByLabelText('View details for Architecture notes')[0],
    )
    expect(screen.getByRole('link', { name: 'Open external' })).toHaveAttribute(
      'href',
      resource.url,
    )

    await user.click(screen.getByRole('button', { name: 'Add resource' }))
    expect(
      screen.getByText(/stores the link and coordination metadata/i),
    ).toBeInTheDocument()
    await user.type(screen.getByLabelText('Title'), 'Sprint demo')
    await user.clear(screen.getByLabelText('External URL'))
    await user.type(
      screen.getByLabelText('External URL'),
      'https://example.com/demo',
    )
    await user.selectOptions(screen.getAllByLabelText('Type')[1], 'Demo')
    await user.selectOptions(screen.getByLabelText('Owner'), 'member-1')
    const addButtons = screen.getAllByRole('button', { name: 'Add resource' })
    await user.click(addButtons[addButtons.length - 1])

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([, init]) => {
          if (init?.method !== 'POST') return false
          const body = JSON.parse(String(init.body)) as Record<string, unknown>
          return body.title === 'Sprint demo' && body.type === 'Demo'
        }),
      ).toBe(true),
    )
  })

  it('keeps members in read-only monitoring mode', async () => {
    workspaceRole = 'Member'
    mockApi()
    renderHub()

    expect(
      (await screen.findAllByText('Architecture notes')).length,
    ).toBeGreaterThan(0)
    expect(
      screen.queryByRole('button', { name: 'Add resource' }),
    ).not.toBeInTheDocument()
    expect(
      screen.getByLabelText('Open Architecture notes externally'),
    ).toHaveAttribute('target', '_blank')
  })

  it('requires owner/repository mapping for GitHub resources', async () => {
    const user = userEvent.setup()
    const fetchMock = mockApi()
    renderHub()

    await screen.findAllByText('Architecture notes')
    await user.click(screen.getByRole('button', { name: 'Add resource' }))
    await user.type(screen.getByLabelText('Title'), 'Repository')
    await user.type(
      screen.getByLabelText('External URL'),
      'https://github.com/Btrung-uit/TeamPulse',
    )
    await user.selectOptions(screen.getAllByLabelText('Type')[1], 'GitHub')
    await user.selectOptions(screen.getByLabelText('Owner'), 'member-1')
    const addButtons = screen.getAllByRole('button', { name: 'Add resource' })
    await user.click(addButtons[addButtons.length - 1])

    expect(
      await screen.findByText(
        /enter the github repository as owner\/repository/i,
      ),
    ).toBeInTheDocument()
    expect(
      fetchMock.mock.calls.some(([, init]) => init?.method === 'POST'),
    ).toBe(false)
  })
})
