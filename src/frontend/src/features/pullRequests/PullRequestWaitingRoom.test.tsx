import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { PullRequestWaitingRoomPage } from '@/features/pullRequests/pages/PullRequestWaitingRoomPage'

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

const feed = {
  items: [
    {
      id: 'pr-1',
      repositoryFullName: 'Btrung-uit/TeamPulse',
      number: 42,
      title: 'TP-062 build the waiting room',
      url: 'https://github.com/Btrung-uit/TeamPulse/pull/42',
      authorLogin: 'octocat',
      requestedReviewers: ['github-reviewer'],
      status: 'WaitingForReview',
      checkStatus: 'Passing',
      openedAtUtc: new Date().toISOString(),
      lastActivityAtUtc: new Date().toISOString(),
      tasks: [
        {
          id: 'task-1',
          code: 'TP-062',
          title: 'PR Waiting Room',
          reviewer: {
            userId: 'reviewer-1',
            displayName: 'Team Reviewer',
            email: 'reviewer@example.com',
          },
        },
      ],
    },
  ],
  waitingCount: 1,
  changesCount: 2,
  mergedCount: 3,
}

describe('PR waiting room', () => {
  afterEach(() => vi.restoreAllMocks())

  it('shows reviewer action, linked task, checks and the GitHub deep link', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify(feed), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <PullRequestWaitingRoomPage />
      </MemoryRouter>,
    )

    expect(
      await screen.findByText('TP-062 build the waiting room'),
    ).toBeVisible()
    expect(screen.getByText('Team Reviewer')).toBeVisible()
    expect(screen.getByText('Reviewer action needed')).toBeVisible()
    expect(screen.getByText('Checks passing')).toBeVisible()
    expect(screen.getByText('TP-062')).toBeVisible()
    expect(
      screen.getByRole('link', { name: /open on github/i }),
    ).toHaveAttribute('href', feed.items[0].url)

    await user.click(screen.getByRole('tab', { name: 'Changes' }))
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([url]) =>
          String(url).includes('filter=changes'),
        ),
      ).toBe(true),
    )
  })
})
