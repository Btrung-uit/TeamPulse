import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { AuthContext, type AuthContextValue } from '@/features/auth/AuthContext'
import { NotificationPanel } from '@/features/shell/components/NotificationPanel'
import { WorkspaceSearchDialog } from '@/features/shell/components/WorkspaceSearchDialog'
import {
  WorkspaceContext,
  type WorkspaceContextValue,
} from '@/features/workspaces/WorkspaceContext'

const authContext: AuthContextValue = {
  status: 'authenticated',
  user: {
    id: 'user-1',
    email: 'leader@example.com',
    displayName: 'Leader',
  },
  accessToken: 'access-token',
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}

const workspaceContext: WorkspaceContextValue = {
  status: 'ready',
  workspaces: [
    {
      id: 'workspace-1',
      name: 'Software Engineering',
      role: 'Leader',
      memberCount: 3,
    },
  ],
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

function LocationProbe() {
  const location = useLocation()
  return (
    <output aria-label="Current location">
      {location.pathname}
      {location.search}
    </output>
  )
}

function Providers({ children }: { children: ReactNode }) {
  return (
    <MemoryRouter initialEntries={['/workspaces/workspace-1/projects']}>
      <AuthContext.Provider value={authContext}>
        <WorkspaceContext.Provider value={workspaceContext}>
          {children}
          <LocationProbe />
        </WorkspaceContext.Provider>
      </AuthContext.Provider>
    </MemoryRouter>
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('workspace search', () => {
  it('searches the current workspace and opens a task result', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          projects: [],
          tasks: [
            {
              id: 'task-1',
              projectId: 'project-1',
              projectName: 'TeamPulse MVP',
              code: 'TP-501',
              title: 'Build global search',
              status: 'InProgress',
            },
          ],
          members: [],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    )
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    render(
      <Providers>
        <WorkspaceSearchDialog isOpen onClose={vi.fn()} />
      </Providers>,
    )

    await user.type(screen.getByRole('textbox', { name: 'Search' }), 'TP-501')
    const result = await screen.findByRole('button', {
      name: /TP-501 · Build global search/,
    })
    await user.click(result)

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/workspaces/workspace-1/search?query=TP-501',
      expect.objectContaining({ signal: expect.any(AbortSignal) }),
    )
    expect(screen.getByLabelText('Current location')).toHaveTextContent(
      '/workspaces/workspace-1/projects/project-1/tasks?task=task-1',
    )
  })
})

describe('notifications', () => {
  it('shows the unread count, saves a read receipt and opens the task', async () => {
    const feed = {
      items: [
        {
          id: 'notification-1',
          kind: 'Assignment',
          title: 'Assigned: TP-601',
          message: 'You were assigned to “Notification task”.',
          projectId: 'project-1',
          taskId: 'task-1',
          isRead: false,
          createdAtUtc: '2026-10-21T12:00:00Z',
        },
      ],
      unreadCount: 1,
    }
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'PATCH') {
        return Promise.resolve(new Response(null, { status: 204 }))
      }
      expect(String(input)).toContain(
        '/api/workspaces/workspace-1/notifications',
      )
      return Promise.resolve(
        new Response(JSON.stringify(feed), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      )
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    const onUnreadCountChange = vi.fn()

    render(
      <Providers>
        <NotificationPanel
          isOpen
          onClose={vi.fn()}
          onUnreadCountChange={onUnreadCountChange}
        />
      </Providers>,
    )

    const item = await screen.findByRole('button', {
      name: /Assigned: TP-601/,
    })
    await waitFor(() => expect(onUnreadCountChange).toHaveBeenCalledWith(1))
    await user.click(item)

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/workspaces/workspace-1/notifications/notification-1/read',
        expect.objectContaining({ method: 'PATCH' }),
      ),
    )
    expect(screen.getByLabelText('Current location')).toHaveTextContent(
      '/workspaces/workspace-1/projects/project-1/tasks?task=task-1',
    )
    expect(onUnreadCountChange).toHaveBeenCalledWith(0)
  })
})
