import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { AuthContext, type AuthContextValue } from '@/features/auth/AuthContext'
import { ThemeContext } from '@/app/providers/themeContext'
import {
  ProjectContext,
  type ProjectContextValue,
} from '@/features/projects/ProjectContext'
import { NotificationPanel } from '@/features/shell/components/NotificationPanel'
import { WorkspaceSearchDialog } from '@/features/shell/components/WorkspaceSearchDialog'
import { MobileMorePage } from '@/features/shell/pages/MobileMorePage'
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

const projectContext: ProjectContextValue = {
  status: 'ready',
  projects: [
    {
      id: 'project-1',
      workspaceId: 'workspace-1',
      name: 'TeamPulse MVP',
      description: null,
      isArchived: false,
      createdAtUtc: '2026-10-01T00:00:00Z',
      updatedAtUtc: '2026-10-01T00:00:00Z',
      archivedAtUtc: null,
    },
  ],
  currentProject: {
    id: 'project-1',
    workspaceId: 'workspace-1',
    name: 'TeamPulse MVP',
    description: null,
    isArchived: false,
    createdAtUtc: '2026-10-01T00:00:00Z',
    updatedAtUtc: '2026-10-01T00:00:00Z',
    archivedAtUtc: null,
  },
  error: null,
  createProject: vi.fn(),
  updateProject: vi.fn(),
  archiveProject: vi.fn(),
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

describe('mobile more navigation', () => {
  it('keeps project planning and workspace administration reachable', () => {
    render(
      <MemoryRouter>
        <ThemeContext.Provider value={{ theme: 'light', toggleTheme: vi.fn() }}>
          <AuthContext.Provider value={authContext}>
            <WorkspaceContext.Provider value={workspaceContext}>
              <ProjectContext.Provider value={projectContext}>
                <MobileMorePage />
              </ProjectContext.Provider>
            </WorkspaceContext.Provider>
          </AuthContext.Provider>
        </ThemeContext.Provider>
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: /Projects/ })).toHaveAttribute(
      'href',
      '/workspaces/workspace-1/projects',
    )
    expect(screen.getByRole('link', { name: /Members/ })).toHaveAttribute(
      'href',
      '/workspaces/workspace-1/members',
    )
    expect(screen.getByRole('link', { name: /Task board/ })).toHaveAttribute(
      'href',
      '/workspaces/workspace-1/projects/project-1/tasks?attention=true',
    )
    expect(
      screen.getByRole('link', { name: /PR waiting room/ }),
    ).toHaveAttribute(
      'href',
      '/workspaces/workspace-1/projects/project-1/engineering/pull-requests',
    )
    expect(screen.getByRole('link', { name: /Resource hub/ })).toHaveAttribute(
      'href',
      '/workspaces/workspace-1/projects/project-1/resources',
    )
    expect(
      screen.getByRole('link', { name: /Presentation mode/ }),
    ).toHaveAttribute(
      'href',
      '/workspaces/workspace-1/projects/project-1/presentation',
    )
    expect(
      screen.getByRole('link', { name: /Help & glossary/ }),
    ).toHaveAttribute('href', '/workspaces/workspace-1/help')
  })
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
          externalUrl: null,
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

  it('opens a GitHub notification at its external deep link', async () => {
    const feed = {
      items: [
        {
          id: 'notification-pr',
          kind: 'PullRequestReviewRequested',
          title: 'PR ready for review: TP-062',
          message: 'A linked pull request is ready for review on GitHub.',
          projectId: 'project-1',
          taskId: 'task-1',
          externalUrl: 'https://github.com/Btrung-uit/TeamPulse/pull/42',
          isRead: true,
          createdAtUtc: '2026-10-24T08:00:00Z',
        },
      ],
      unreadCount: 0,
    }
    vi.stubGlobal(
      'fetch',
      vi.fn().mockImplementation(() =>
        Promise.resolve(
          new Response(JSON.stringify(feed), {
            status: 200,
            headers: { 'Content-Type': 'application/json' },
          }),
        ),
      ),
    )
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    const user = userEvent.setup()

    render(
      <Providers>
        <NotificationPanel
          isOpen
          onClose={vi.fn()}
          onUnreadCountChange={vi.fn()}
        />
      </Providers>,
    )

    await user.click(
      await screen.findByRole('button', {
        name: /PR ready for review: TP-062/,
      }),
    )
    expect(open).toHaveBeenCalledWith(
      feed.items[0].externalUrl,
      '_blank',
      'noopener,noreferrer',
    )
  })
})
