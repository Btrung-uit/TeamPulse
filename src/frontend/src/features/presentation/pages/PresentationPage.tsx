import { useEffect, useMemo, useState, type CSSProperties } from 'react'
import {
  ArrowLeft,
  ArrowUpRight,
  CheckCircle2,
  CircleAlert,
  ExternalLink,
  FileText,
  GitBranch,
  Maximize2,
  Minimize2,
  MonitorPlay,
  Presentation,
  Users,
} from 'lucide-react'
import { Link } from 'react-router-dom'

import { Button } from '@/components/ui'
import { useDashboard } from '@/features/dashboard/dashboardQueries'
import { projectPath } from '@/features/projects/projectPaths'
import { useProject } from '@/features/projects/useProject'
import { useResources } from '@/features/resources/resourceQueries'
import type { ProjectResource, ResourceType } from '@/features/resources/types'
import { memberInitials } from '@/features/tasks/taskBoardModel'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

const resourceOrder: Record<ResourceType, number> = {
  GitHub: 0,
  Demo: 1,
  Canva: 2,
  GoogleDocs: 3,
  GoogleDrive: 4,
  Other: 5,
}

const resourceMeta: Record<
  ResourceType,
  { label: string; icon: typeof GitBranch }
> = {
  GitHub: { label: 'Repository', icon: GitBranch },
  Demo: { label: 'Live demo', icon: MonitorPlay },
  Canva: { label: 'Slides / design', icon: Presentation },
  GoogleDocs: { label: 'Documentation', icon: FileText },
  GoogleDrive: { label: 'Project files', icon: FileText },
  Other: { label: 'Project resource', icon: ExternalLink },
}

function currentPhase(kpis: {
  totalTasks: number
  doneTasks: number
  inReviewTasks: number
  blockedTasks: number
}) {
  if (kpis.totalTasks === 0) return 'Planning'
  if (kpis.doneTasks === kpis.totalTasks) return 'Completed'
  if (kpis.blockedTasks > 0) return 'Delivery · attention needed'
  if (kpis.inReviewTasks > 0) return 'Review and validation'
  return 'Active delivery'
}

function PresentationLoading() {
  return (
    <main
      className="presentation-mode presentation-mode--state"
      role="status"
      aria-live="polite"
      aria-label="Loading presentation"
    >
      <div className="presentation-state-card">
        <span className="presentation-loader" aria-hidden="true" />
        <p>Preparing the project story…</p>
      </div>
    </main>
  )
}

function ResourceCard({ resource }: { resource: ProjectResource }) {
  const meta = resourceMeta[resource.type]
  const Icon = meta.icon
  return (
    <a
      className="presentation-resource"
      href={resource.url}
      target="_blank"
      rel="noreferrer"
    >
      <span className="presentation-resource__icon">
        <Icon size={22} aria-hidden="true" />
      </span>
      <span>
        <small>{meta.label}</small>
        <strong>{resource.title}</strong>
        {resource.linkedTask && <em>{resource.linkedTask.code}</em>}
      </span>
      <ArrowUpRight size={19} aria-hidden="true" />
    </a>
  )
}

export function PresentationPage() {
  const { currentWorkspace } = useWorkspace()
  const { currentProject } = useProject()
  const workspaceId = currentWorkspace!.id
  const projectId = currentProject!.id
  const projectBase = projectPath(workspaceId, projectId)
  const dashboardQuery = useDashboard(workspaceId, projectId)
  const resourcesQuery = useResources(workspaceId, projectId, {})
  const [isFullscreen, setIsFullscreen] = useState(
    Boolean(document.fullscreenElement),
  )

  useEffect(() => {
    const update = () => setIsFullscreen(Boolean(document.fullscreenElement))
    document.addEventListener('fullscreenchange', update)
    return () => document.removeEventListener('fullscreenchange', update)
  }, [])

  const resources = useMemo(
    () =>
      [...(resourcesQuery.data ?? [])].sort(
        (left, right) =>
          resourceOrder[left.type] - resourceOrder[right.type] ||
          left.title.localeCompare(right.title),
      ),
    [resourcesQuery.data],
  )

  if (dashboardQuery.isLoading || resourcesQuery.isLoading)
    return <PresentationLoading />

  if (
    dashboardQuery.isError ||
    resourcesQuery.isError ||
    !dashboardQuery.data
  ) {
    return (
      <main className="presentation-mode presentation-mode--state">
        <div className="presentation-state-card" role="alert">
          <CircleAlert size={28} aria-hidden="true" />
          <h1>Presentation data could not be loaded.</h1>
          <p>Return to the project and try again when the API is available.</p>
          <Link
            className="ui-button ui-button--secondary ui-button--md"
            to={projectBase}
          >
            Back to project
          </Link>
        </div>
      </main>
    )
  }

  const dashboard = dashboardQuery.data
  const totalAcceptedEvidence = dashboard.contributions.reduce(
    (total, member) => total + member.acceptedEvidence,
    0,
  )
  const totalReviews = dashboard.contributions.reduce(
    (total, member) => total + member.reviews,
    0,
  )

  async function toggleFullscreen() {
    if (document.fullscreenElement) await document.exitFullscreen()
    else await document.documentElement.requestFullscreen()
  }

  return (
    <main className="presentation-mode" id="main-content">
      <header className="presentation-toolbar">
        <Link to={projectBase}>
          <ArrowLeft size={18} aria-hidden="true" />
          Exit presentation
        </Link>
        <nav aria-label="Presentation sections">
          <a href="#story">Outcome</a>
          <a href="#progress">Progress</a>
          <a href="#team">Team</a>
          <a href="#resources">Resources</a>
        </nav>
        <Button
          variant="ghost"
          size="sm"
          onClick={() => void toggleFullscreen()}
          leadingIcon={
            isFullscreen ? <Minimize2 size={17} /> : <Maximize2 size={17} />
          }
        >
          {isFullscreen ? 'Exit full screen' : 'Full screen'}
        </Button>
      </header>

      <section className="presentation-scene presentation-hero" id="story">
        <div className="presentation-hero__copy">
          <p className="presentation-kicker">
            {currentWorkspace!.name} · Project outcome
          </p>
          <h1>{currentProject!.name}</h1>
          <p className="presentation-outcome">
            {currentProject!.description ||
              'A focused collaboration project with transparent delivery, evidence and review.'}
          </p>
          <div className="presentation-phase">
            <span>Current phase</span>
            <strong>{currentPhase(dashboard.kpis)}</strong>
          </div>
        </div>
        <div
          className="presentation-hero__progress"
          style={
            {
              '--presentation-progress': `${dashboard.kpis.progressPercent}%`,
            } as CSSProperties
          }
        >
          <div>
            <strong>{dashboard.kpis.progressPercent}%</strong>
            <span>complete</span>
          </div>
          <p>
            {dashboard.kpis.doneTasks} of {dashboard.kpis.totalTasks} tasks
            accepted
          </p>
        </div>
        <span className="presentation-slide-number">01 / 04</span>
      </section>

      <section className="presentation-scene" id="progress">
        <div className="presentation-section-heading">
          <div>
            <p className="presentation-kicker">Delivery pulse</p>
            <h2>Progress at a glance</h2>
          </div>
          <p>Live from the TeamPulse task workflow</p>
        </div>
        <div className="presentation-kpis">
          <article>
            <strong>{dashboard.kpis.doneTasks}</strong>
            <span>Accepted tasks</span>
            <CheckCircle2 aria-hidden="true" />
          </article>
          <article>
            <strong>{dashboard.kpis.openTasks}</strong>
            <span>Open tasks</span>
            <Presentation aria-hidden="true" />
          </article>
          <article
            className={dashboard.kpis.inReviewTasks ? 'is-highlighted' : ''}
          >
            <strong>{dashboard.kpis.inReviewTasks}</strong>
            <span>Waiting review</span>
            <Users aria-hidden="true" />
          </article>
          <article className={dashboard.kpis.blockedTasks ? 'is-risk' : ''}>
            <strong>{dashboard.kpis.blockedTasks}</strong>
            <span>Blocked</span>
            <CircleAlert aria-hidden="true" />
          </article>
        </div>
        <div className="presentation-status-list">
          {dashboard.statusBreakdown
            .filter((item) => item.count > 0)
            .map((item) => (
              <div key={item.status}>
                <span>{item.status.replace(/([a-z])([A-Z])/g, '$1 $2')}</span>
                <div>
                  <i
                    style={{
                      width: `${Math.max(6, (item.count / Math.max(1, dashboard.kpis.totalTasks)) * 100)}%`,
                    }}
                  />
                </div>
                <strong>{item.count}</strong>
              </div>
            ))}
        </div>
        {dashboard.kpis.blockedTasks > 0 && (
          <p className="presentation-risk">
            <CircleAlert size={18} />
            {dashboard.kpis.blockedTasks} blocked task(s) need ownership or
            dependency follow-up.
          </p>
        )}
        <span className="presentation-slide-number">02 / 04</span>
      </section>

      <section className="presentation-scene" id="team">
        <div className="presentation-section-heading">
          <div>
            <p className="presentation-kicker">Contribution evidence</p>
            <h2>The team behind the outcome</h2>
          </div>
          <p>
            {totalAcceptedEvidence} accepted evidence · {totalReviews} reviews
          </p>
        </div>
        <div className="presentation-team-grid">
          {dashboard.contributions.map((member) => (
            <article key={member.userId}>
              <span className="presentation-avatar" aria-hidden="true">
                {memberInitials(member.displayName)}
              </span>
              <div>
                <h3>{member.displayName}</h3>
                <p>{member.role}</p>
              </div>
              <dl>
                <div>
                  <dt>Tasks</dt>
                  <dd>{member.acceptedTasks}</dd>
                </div>
                <div>
                  <dt>Evidence</dt>
                  <dd>{member.acceptedEvidence}</dd>
                </div>
                <div>
                  <dt>Reviews</dt>
                  <dd>{member.reviews}</dd>
                </div>
              </dl>
            </article>
          ))}
        </div>
        {dashboard.contributions.length === 0 && (
          <p className="presentation-empty">
            Contribution evidence will appear after tasks are accepted.
          </p>
        )}
        <p className="presentation-footnote">
          Contribution is shown through accepted work, evidence and review—not
          raw commit counts.
        </p>
        <span className="presentation-slide-number">03 / 04</span>
      </section>

      <section className="presentation-scene" id="resources">
        <div className="presentation-section-heading">
          <div>
            <p className="presentation-kicker">Project proof</p>
            <h2>Repository, demo and key resources</h2>
          </div>
          <p>Open any resource in a new tab</p>
        </div>
        {resources.length > 0 ? (
          <div className="presentation-resources">
            {resources.map((resource) => (
              <ResourceCard resource={resource} key={resource.id} />
            ))}
          </div>
        ) : (
          <p className="presentation-empty">
            No project resources have been linked yet.
          </p>
        )}
        <footer className="presentation-closing">
          <span>TeamPulse</span>
          <strong>{currentProject!.name}</strong>
          <p>From task → evidence → review → outcome.</p>
        </footer>
        <span className="presentation-slide-number">04 / 04</span>
      </section>
    </main>
  )
}

export default PresentationPage
