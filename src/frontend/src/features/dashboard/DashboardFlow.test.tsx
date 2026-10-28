import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import DashboardPage from '@/features/dashboard/pages/DashboardPage'

vi.mock('@/features/auth/useAuth', () => ({
  useAuth: () => ({ accessToken: 'test-token' }),
}))
vi.mock('@/features/workspaces/useWorkspace', () => ({
  useWorkspace: () => ({
    currentWorkspace: { id: 'workspace-1', role: 'Leader' },
  }),
}))
vi.mock('@/features/projects/useProject', () => ({
  useProject: () => ({
    currentProject: { id: 'project-1', name: 'TeamPulse MVP' },
  }),
}))

const dashboard = {
  kpis: {
    totalTasks: 4,
    doneTasks: 1,
    openTasks: 3,
    inReviewTasks: 1,
    blockedTasks: 1,
    dueSoonTasks: 1,
    progressPercent: 25,
  },
  statusBreakdown: [
    { status: 'Todo', count: 1 },
    { status: 'InProgress', count: 0 },
    { status: 'EvidenceLinked', count: 0 },
    { status: 'InReview', count: 1 },
    { status: 'NeedsChanges', count: 0 },
    { status: 'Blocked', count: 1 },
    { status: 'Done', count: 1 },
  ],
  workload: [
    {
      userId: 'member-1',
      displayName: 'Bao Trung',
      role: 'Member',
      openTasks: 3,
      blockedTasks: 1,
      dueSoonTasks: 1,
    },
  ],
  myPriorityTasks: [
    {
      id: 'task-1',
      code: 'TP-031',
      title: 'Build dashboard',
      status: 'InReview',
      priority: 'High',
      dueOn: '2026-10-20',
    },
  ],
  recentActivity: [
    {
      id: 'activity-1',
      actorUserId: 'member-1',
      actorDisplayName: 'Bao Trung',
      action: 'TaskEvidenceAdded',
      entityId: 'task-1',
      taskCode: 'TP-031',
      previousValue: null,
      newValue: null,
      occurredAtUtc: '2026-10-17T00:00:00Z',
    },
  ],
  contributions: [
    {
      userId: 'member-1',
      displayName: 'Bao Trung',
      role: 'Member',
      acceptedTasks: 1,
      acceptedEvidence: 1,
      reviews: 2,
      lastContributedAtUtc: '2026-10-17T00:00:00Z',
    },
  ],
}

function renderDashboard() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('Dashboard flow', () => {
  afterEach(() => vi.restoreAllMocks())

  it('prioritizes actionable work and links the three summary signals to task filters', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify(dashboard), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )

    renderDashboard()

    expect(await screen.findByText('Build dashboard')).toBeInTheDocument()
    expect(
      screen.getByText('Accepted work, evidence and reviews'),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: /View my tasks/i }),
    ).toHaveAttribute(
      'href',
      '/workspaces/workspace-1/projects/project-1/my-tasks',
    )
    expect(
      screen.getByRole('link', { name: /Project progress/i }),
    ).toHaveAttribute(
      'href',
      '/workspaces/workspace-1/projects/project-1/tasks?view=open',
    )
    expect(
      screen.getByRole('link', { name: /Waiting review/i }),
    ).toHaveAttribute(
      'href',
      '/workspaces/workspace-1/projects/project-1/tasks?status=InReview',
    )
    expect(
      screen.getByRole('link', { name: /Needs attention/i }),
    ).toHaveAttribute(
      'href',
      '/workspaces/workspace-1/projects/project-1/tasks?attention=true',
    )
    expect(
      screen.getByText('No score or commit-count ranking'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Open tasks/i })).toBeNull()
    expect(screen.queryByText('Status breakdown')).toBeNull()
    expect(screen.queryByText('Recent activity')).toBeNull()
    expect(screen.queryByRole('link', { name: /Present project/i })).toBeNull()
    expect(
      screen.getByRole('link', { name: 'How contribution is counted' }),
    ).toHaveAttribute('href', '/workspaces/workspace-1/help#contribution')
  })

  it('shows the first-use state when a project has no tasks', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          ...dashboard,
          kpis: { ...dashboard.kpis, totalTasks: 0 },
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    renderDashboard()
    expect(
      await screen.findByText('No project activity yet'),
    ).toBeInTheDocument()
  })
})
