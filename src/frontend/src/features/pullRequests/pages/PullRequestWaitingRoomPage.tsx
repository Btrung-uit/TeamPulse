import { useCallback, useEffect, useState, type ReactNode } from 'react'
import {
  AlertCircle,
  CheckCircle2,
  CircleHelp,
  CircleDot,
  Clock3,
  ExternalLink,
  GitPullRequest,
  RefreshCw,
  UserRoundCheck,
} from 'lucide-react'
import { Link } from 'react-router-dom'

import { Button } from '@/components/ui/Button/Button'
import { EmptyState } from '@/components/ui/EmptyState/EmptyState'
import { useAuth } from '@/features/auth/useAuth'
import { useProject } from '@/features/projects/useProject'
import { listPullRequests } from '@/features/pullRequests/pullRequestApi'
import type {
  PullRequestCheckStatus,
  PullRequestFeed,
  PullRequestFilter,
  PullRequestItem,
  PullRequestStatus,
} from '@/features/pullRequests/types'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

const emptyFeed: PullRequestFeed = {
  items: [],
  waitingCount: 0,
  changesCount: 0,
  mergedCount: 0,
}

const filters: Array<{ value: PullRequestFilter; label: string }> = [
  { value: 'all', label: 'All' },
  { value: 'waiting', label: 'Waiting' },
  { value: 'changes', label: 'Changes' },
  { value: 'merged', label: 'Merged' },
]

export function PullRequestWaitingRoomPage() {
  const { accessToken } = useAuth()
  const { currentWorkspace } = useWorkspace()
  const { currentProject } = useProject()
  const [filter, setFilter] = useState<PullRequestFilter>('waiting')
  const [feed, setFeed] = useState<PullRequestFeed>(emptyFeed)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string>()

  const load = useCallback(
    async (signal?: AbortSignal, quiet = false) => {
      if (!accessToken || !currentWorkspace || !currentProject) return
      if (!quiet) setIsLoading(true)
      setError(undefined)
      try {
        setFeed(
          await listPullRequests(
            accessToken,
            currentWorkspace.id,
            currentProject.id,
            filter,
            signal,
          ),
        )
      } catch (caught) {
        if (caught instanceof DOMException && caught.name === 'AbortError')
          return
        setError('Pull requests are unavailable right now.')
      } finally {
        if (!quiet) setIsLoading(false)
      }
    },
    [accessToken, currentProject, currentWorkspace, filter],
  )

  useEffect(() => {
    const controller = new AbortController()
    void load(controller.signal)
    const poll = window.setInterval(() => void load(undefined, true), 30_000)
    return () => {
      controller.abort()
      window.clearInterval(poll)
    }
  }, [load])

  return (
    <main className="pr-waiting-room">
      <header className="page-header pr-waiting-room__header">
        <div>
          <p className="eyebrow">Engineering</p>
          <h1>PR waiting room</h1>
          <p>
            See what needs review, changes, or merge. Code review stays on
            GitHub.
          </p>
          <Link
            className="context-help-link"
            to={`/workspaces/${currentWorkspace!.id}/help#pr-waiting-room`}
          >
            <CircleHelp aria-hidden="true" size={15} />
            How the waiting room works
          </Link>
        </div>
        <Button
          variant="secondary"
          leadingIcon={<RefreshCw size={17} />}
          isLoading={isLoading}
          loadingLabel="Refreshing pull requests"
          onClick={() => void load()}
        >
          Refresh
        </Button>
      </header>

      <section className="pr-summary" aria-label="Pull request summary">
        <SummaryCard
          label="Waiting"
          value={feed.waitingCount}
          icon={<Clock3 />}
        />
        <SummaryCard
          label="Changes needed"
          value={feed.changesCount}
          icon={<AlertCircle />}
        />
        <SummaryCard
          label="Merged"
          value={feed.mergedCount}
          icon={<CheckCircle2 />}
        />
      </section>

      <div
        className="pr-filter-tabs"
        role="tablist"
        aria-label="Filter pull requests"
      >
        {filters.map((item) => (
          <button
            key={item.value}
            type="button"
            role="tab"
            aria-selected={filter === item.value}
            className={filter === item.value ? 'is-active' : undefined}
            onClick={() => setFilter(item.value)}
          >
            {item.label}
          </button>
        ))}
      </div>

      {error && (
        <section className="pr-state" role="alert">
          <AlertCircle aria-hidden="true" />
          <div>
            <strong>Unable to load the waiting room</strong>
            <p>{error}</p>
          </div>
          <Button variant="secondary" onClick={() => void load()}>
            Try again
          </Button>
        </section>
      )}

      {!error && !isLoading && feed.items.length === 0 && (
        <EmptyState
          icon={<GitPullRequest />}
          title="No pull requests in this view"
          description="Reference a task code such as TP-123 in a GitHub PR title or body, then deliver the repository webhook."
        />
      )}

      {!error && feed.items.length > 0 && (
        <section className="pr-list" aria-live="polite">
          {feed.items.map((pullRequest) => (
            <PullRequestCard key={pullRequest.id} pullRequest={pullRequest} />
          ))}
        </section>
      )}
    </main>
  )
}

function SummaryCard({
  label,
  value,
  icon,
}: {
  label: string
  value: number
  icon: ReactNode
}) {
  return (
    <article className="pr-summary__card">
      <span aria-hidden="true">{icon}</span>
      <div>
        <strong>{value}</strong>
        <p>{label}</p>
      </div>
    </article>
  )
}

function PullRequestCard({ pullRequest }: { pullRequest: PullRequestItem }) {
  const teamReviewers = Array.from(
    new Set(
      pullRequest.tasks
        .map((task) => task.reviewer?.displayName)
        .filter((name): name is string => Boolean(name)),
    ),
  )
  const reviewers =
    teamReviewers.length > 0 ? teamReviewers : pullRequest.requestedReviewers

  return (
    <article className="pr-card">
      <div className="pr-card__main">
        <div className="pr-card__identity">
          <span>
            {pullRequest.repositoryFullName} #{pullRequest.number}
          </span>
          <small>{formatAge(pullRequest.openedAtUtc)}</small>
        </div>
        <h2>{pullRequest.title}</h2>
        <div className="pr-card__badges">
          <StatusBadge status={pullRequest.status} />
          <CheckBadge status={pullRequest.checkStatus} />
        </div>
        <div className="pr-card__tasks">
          {pullRequest.tasks.length === 0 ? (
            <span className="pr-card__muted">No matching TeamPulse task</span>
          ) : (
            pullRequest.tasks.map((task) => (
              <span key={task.id} title={task.title}>
                {task.code}
              </span>
            ))
          )}
        </div>
      </div>

      <div className="pr-card__meta">
        <div>
          <UserRoundCheck aria-hidden="true" size={17} />
          <span>
            {reviewers.length > 0
              ? reviewers.join(', ')
              : 'No reviewer assigned'}
          </span>
        </div>
        <div>
          <CircleDot aria-hidden="true" size={17} />
          <span>{actionLabel(pullRequest)}</span>
        </div>
        <a href={pullRequest.url} target="_blank" rel="noreferrer">
          Open on GitHub
          <ExternalLink aria-hidden="true" size={16} />
        </a>
      </div>
    </article>
  )
}

function StatusBadge({ status }: { status: PullRequestStatus }) {
  const labels: Record<PullRequestStatus, string> = {
    Draft: 'Draft',
    WaitingForReview: 'Waiting for review',
    ChangesRequested: 'Changes requested',
    ReadyToMerge: 'Ready to merge',
    Merged: 'Merged',
    Closed: 'Closed',
  }
  return (
    <span className={`pr-badge pr-badge--${status}`}>{labels[status]}</span>
  )
}

function CheckBadge({ status }: { status: PullRequestCheckStatus }) {
  const labels: Record<PullRequestCheckStatus, string> = {
    Unknown: 'Checks not reported',
    Pending: 'Checks pending',
    Passing: 'Checks passing',
    Failing: 'Checks failing',
  }
  return (
    <span className={`pr-check pr-check--${status}`}>{labels[status]}</span>
  )
}

function actionLabel(pullRequest: PullRequestItem) {
  if (pullRequest.checkStatus === 'Failing') return 'Fix failing checks'
  if (pullRequest.status === 'ChangesRequested') return 'Author action needed'
  if (pullRequest.status === 'ReadyToMerge') return 'Ready for leader to merge'
  if (pullRequest.status === 'WaitingForReview') return 'Reviewer action needed'
  if (pullRequest.status === 'Draft') return 'Author is still working'
  if (pullRequest.status === 'Merged') return 'No action needed'
  return 'Closed without merge'
}

function formatAge(value: string) {
  const days = Math.max(
    0,
    Math.floor((Date.now() - new Date(value).getTime()) / 86_400_000),
  )
  if (days === 0) return 'Opened today'
  return `Open for ${days} day${days === 1 ? '' : 's'}`
}
