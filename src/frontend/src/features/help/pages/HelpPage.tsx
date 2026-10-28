import {
  ArrowRight,
  GitPullRequest,
  LayoutList,
  Presentation,
} from 'lucide-react'
import { Link } from 'react-router-dom'

import { useProject } from '@/features/projects/useProject'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

const workflow = [
  ['Task', 'Define the work, owner, reviewer, deadline and expected outcome.'],
  [
    'Evidence',
    'Link a commit, pull request, document, design or demo that proves the work.',
  ],
  ['Review', 'A reviewer accepts the result or requests specific changes.'],
  [
    'Contribution',
    'Accepted tasks, evidence and completed reviews become the contribution record.',
  ],
  ['Presentation', 'Turn verified project activity into a clear demo story.'],
]

export function HelpPage() {
  const { currentWorkspace } = useWorkspace()
  const { projects } = useProject()
  const project = projects.find((item) => !item.isArchived)
  const workspaceBase = `/workspaces/${currentWorkspace!.id}`
  const projectBase = project
    ? `${workspaceBase}/projects/${project.id}`
    : undefined

  return (
    <main className="help-page">
      <header className="help-page__header">
        <p className="eyebrow">Product guidance</p>
        <h1>Help & glossary</h1>
        <p>
          Follow TeamPulse from assigned work to verified contribution, then use
          the glossary when a workflow label is unfamiliar.
        </p>
      </header>

      <section className="help-section" aria-labelledby="help-workflow-title">
        <div className="help-section__heading">
          <div>
            <p className="eyebrow">Core workflow</p>
            <h2 id="help-workflow-title">From task to project story</h2>
          </div>
          <span>Work moves forward only when the evidence is reviewable.</span>
        </div>
        <ol className="help-workflow">
          {workflow.map(([title, description], index) => (
            <li key={title}>
              <span aria-hidden="true">{index + 1}</span>
              <div>
                <strong>{title}</strong>
                <p>{description}</p>
              </div>
            </li>
          ))}
        </ol>
      </section>

      <section className="help-section" aria-labelledby="help-guides-title">
        <div className="help-section__heading">
          <div>
            <p className="eyebrow">Quick guides</p>
            <h2 id="help-guides-title">Go to the work</h2>
          </div>
        </div>
        <div className="help-guide-list">
          <Link to={`${workspaceBase}/projects`}>
            <LayoutList aria-hidden="true" size={19} />
            <span>
              <strong>Choose a project</strong>
              <small>Open an active project or create the first one.</small>
            </span>
            <ArrowRight aria-hidden="true" size={17} />
          </Link>
          {projectBase && (
            <>
              <Link to={`${projectBase}/tasks?attention=true`}>
                <LayoutList aria-hidden="true" size={19} />
                <span>
                  <strong>Review work needing attention</strong>
                  <small>Open blocked, overdue and review-ready tasks.</small>
                </span>
                <ArrowRight aria-hidden="true" size={17} />
              </Link>
              <Link to={`${projectBase}/engineering/pull-requests`}>
                <GitPullRequest aria-hidden="true" size={19} />
                <span>
                  <strong>Check pull requests</strong>
                  <small>Find work waiting for review or changes.</small>
                </span>
                <ArrowRight aria-hidden="true" size={17} />
              </Link>
              <Link to={`${projectBase}/presentation`}>
                <Presentation aria-hidden="true" size={19} />
                <span>
                  <strong>Prepare a project demo</strong>
                  <small>Present progress, evidence and contribution.</small>
                </span>
                <ArrowRight aria-hidden="true" size={17} />
              </Link>
            </>
          )}
        </div>
      </section>

      <section className="help-section" aria-labelledby="help-glossary-title">
        <div className="help-section__heading">
          <div>
            <p className="eyebrow">Glossary</p>
            <h2 id="help-glossary-title">Terms used in TeamPulse</h2>
          </div>
        </div>
        <dl className="help-glossary">
          <div id="task-statuses">
            <dt>Task statuses</dt>
            <dd>
              Todo → In progress → Evidence linked → In review → Done. Needs
              changes sends reviewed work back for revision; Blocked means an
              external issue prevents progress.
            </dd>
          </div>
          <div id="evidence">
            <dt>Evidence and resource</dt>
            <dd>
              Evidence proves that a specific task was completed. A resource is
              a reusable project link or document and does not count as task
              proof by itself.
            </dd>
          </div>
          <div id="pr-waiting-room">
            <dt>PR waiting room</dt>
            <dd>
              A read-only view of pull requests waiting for review, changes or
              merge. Review and merge actions still happen on GitHub.
            </dd>
          </div>
          <div id="contribution">
            <dt>Contribution</dt>
            <dd>
              A factual summary of accepted tasks, accepted evidence and reviews
              completed. It is not an automatic quality score or a ranking by
              commit count.
            </dd>
          </div>
          <div id="presentation-mode">
            <dt>Presentation Mode</dt>
            <dd>
              A focused project view for demos that turns current progress,
              evidence and contribution into a shareable story.
            </dd>
          </div>
        </dl>
      </section>
    </main>
  )
}
