import { useMemo, useState, type FormEvent } from 'react'
import { Archive, ArrowUpRight, FolderKanban, Plus, Search } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'

import { Button } from '@/components/ui/Button/Button'
import { EmptyState } from '@/components/ui/EmptyState/EmptyState'
import { Input, Textarea } from '@/components/ui/Input/Input'
import { Modal } from '@/components/ui/Modal/Modal'
import { useToast } from '@/components/ui/Toast/useToast'
import { ProjectApiError } from '@/features/projects/projectApi'
import { projectPath } from '@/features/projects/projectPaths'
import type { ProjectSummary } from '@/features/projects/types'
import { useProject } from '@/features/projects/useProject'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

function updatedLabel(value: string) {
  return new Intl.DateTimeFormat('en', {
    dateStyle: 'medium',
  }).format(new Date(value))
}

interface ProjectCardProps {
  project: ProjectSummary
  workspaceId: string
  canManage: boolean
  onArchive: (project: ProjectSummary) => void
}

function ProjectCard({
  project,
  workspaceId,
  canManage,
  onArchive,
}: ProjectCardProps) {
  return (
    <article
      className={`project-card${project.isArchived ? ' is-archived' : ''}`}
    >
      <Link
        className="project-card__link"
        to={projectPath(workspaceId, project.id)}
      >
        <span className="project-card__mark" aria-hidden="true">
          {project.name.charAt(0).toUpperCase()}
        </span>
        <div className="project-card__content">
          <div className="project-card__heading">
            <h3>{project.name}</h3>
            {project.isArchived && <span>Archived</span>}
          </div>
          <p>{project.description || 'No description has been added yet.'}</p>
          <small>Updated {updatedLabel(project.updatedAtUtc)}</small>
        </div>
        <ArrowUpRight aria-hidden="true" size={18} />
      </Link>
      {canManage && !project.isArchived && (
        <Button
          className="project-card__archive"
          iconOnly
          variant="ghost"
          size="sm"
          aria-label={`Archive ${project.name}`}
          title={`Archive ${project.name}`}
          onClick={() => onArchive(project)}
        >
          <Archive aria-hidden="true" size={16} />
        </Button>
      )}
    </article>
  )
}

export function ProjectListPage() {
  const navigate = useNavigate()
  const { showToast } = useToast()
  const { currentWorkspace } = useWorkspace()
  const { status, projects, error, createProject, archiveProject, reload } =
    useProject()
  const [query, setQuery] = useState('')
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [nameError, setNameError] = useState<string>()
  const [formError, setFormError] = useState<string>()
  const [isSaving, setIsSaving] = useState(false)
  const [archiveTarget, setArchiveTarget] = useState<ProjectSummary | null>(
    null,
  )
  const [isArchiving, setIsArchiving] = useState(false)
  const canManage = currentWorkspace?.role === 'Leader'
  const normalizedQuery = query.trim().toLocaleLowerCase()

  const filteredProjects = useMemo(
    () =>
      projects.filter((project) => {
        if (!normalizedQuery) return true
        return [project.name, project.description]
          .filter(Boolean)
          .some((value) => value!.toLocaleLowerCase().includes(normalizedQuery))
      }),
    [normalizedQuery, projects],
  )
  const activeProjects = filteredProjects.filter(
    (project) => !project.isArchived,
  )
  const archivedProjects = filteredProjects.filter(
    (project) => project.isArchived,
  )

  function closeCreateModal() {
    setIsCreateOpen(false)
    setName('')
    setDescription('')
    setNameError(undefined)
    setFormError(undefined)
  }

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const normalizedName = name.trim()
    if (!normalizedName) {
      setNameError('Enter a project name.')
      return
    }

    setIsSaving(true)
    setNameError(undefined)
    setFormError(undefined)
    try {
      const project = await createProject({
        name: normalizedName,
        description: description.trim() || undefined,
      })
      closeCreateModal()
      showToast({
        title: 'Project created',
        description: project.name,
        variant: 'success',
      })
      navigate(projectPath(project.workspaceId, project.id))
    } catch (caught) {
      if (caught instanceof ProjectApiError) {
        setNameError(caught.body.errors?.name?.[0])
        setFormError(caught.body.message)
      } else {
        setFormError('Unable to create the project. Please try again.')
      }
    } finally {
      setIsSaving(false)
    }
  }

  async function confirmArchive() {
    if (!archiveTarget) return
    setIsArchiving(true)
    try {
      await archiveProject(archiveTarget.id)
      showToast({
        title: 'Project archived',
        description: `${archiveTarget.name} remains available in the archived section.`,
        variant: 'success',
      })
      setArchiveTarget(null)
    } catch {
      showToast({
        title: 'Archive failed',
        description: 'The project was not changed. Please try again.',
        variant: 'error',
      })
    } finally {
      setIsArchiving(false)
    }
  }

  if (status === 'idle' || status === 'loading') {
    return (
      <div
        className="project-list-skeleton"
        role="status"
        aria-live="polite"
        aria-label="Loading projects"
      />
    )
  }

  if (status === 'error') {
    return (
      <section className="project-route-state" role="alert">
        <p className="eyebrow">Connection issue</p>
        <h1>We could not load your projects.</h1>
        <p>{error}</p>
        <Button onClick={() => void reload()}>Try again</Button>
      </section>
    )
  }

  return (
    <div className="project-list-page">
      <header className="project-list-header">
        <div>
          <p className="eyebrow">{currentWorkspace?.name}</p>
          <h1>Projects</h1>
          <p>
            Choose a project to continue or create a focused home for new work.
          </p>
        </div>
        {canManage && (
          <Button
            leadingIcon={<Plus size={17} />}
            onClick={() => setIsCreateOpen(true)}
          >
            New project
          </Button>
        )}
      </header>

      {projects.length > 0 && (
        <div className="project-list-toolbar">
          <Input
            label="Search projects"
            placeholder="Search by name or description"
            startIcon={<Search size={17} />}
            value={query}
            onChange={(event) => setQuery(event.target.value)}
          />
        </div>
      )}

      {projects.length === 0 ? (
        <EmptyState
          icon={<FolderKanban size={24} />}
          title="No projects yet"
          description={
            canManage
              ? 'Create the first project to establish a shared context for tasks, resources and progress.'
              : 'A workspace leader has not created a project yet.'
          }
          primaryAction={
            canManage ? (
              <Button
                leadingIcon={<Plus size={17} />}
                onClick={() => setIsCreateOpen(true)}
              >
                Create first project
              </Button>
            ) : undefined
          }
        />
      ) : filteredProjects.length === 0 ? (
        <EmptyState
          icon={<Search size={24} />}
          title="No matching projects"
          description="Try a different name or clear the current search."
          variant="filtered"
          primaryAction={
            <Button variant="secondary" onClick={() => setQuery('')}>
              Clear search
            </Button>
          }
        />
      ) : (
        <div className="project-groups">
          {activeProjects.length > 0 && (
            <section
              className="project-group"
              aria-labelledby="active-projects-title"
            >
              <div className="project-group__heading">
                <h2 id="active-projects-title">Active</h2>
                <span>{activeProjects.length}</span>
              </div>
              <div className="project-grid">
                {activeProjects.map((project) => (
                  <ProjectCard
                    key={project.id}
                    project={project}
                    workspaceId={currentWorkspace!.id}
                    canManage={canManage}
                    onArchive={setArchiveTarget}
                  />
                ))}
              </div>
            </section>
          )}
          {archivedProjects.length > 0 && (
            <section
              className="project-group"
              aria-labelledby="archived-projects-title"
            >
              <div className="project-group__heading">
                <h2 id="archived-projects-title">Archived</h2>
                <span>{archivedProjects.length}</span>
              </div>
              <div className="project-grid">
                {archivedProjects.map((project) => (
                  <ProjectCard
                    key={project.id}
                    project={project}
                    workspaceId={currentWorkspace!.id}
                    canManage={canManage}
                    onArchive={setArchiveTarget}
                  />
                ))}
              </div>
            </section>
          )}
        </div>
      )}

      <Modal
        isOpen={isCreateOpen}
        onClose={closeCreateModal}
        title="Create project"
        description="Projects keep task, evidence and reporting context separate."
        isPending={isSaving}
        footer={
          <>
            <Button
              variant="secondary"
              disabled={isSaving}
              onClick={closeCreateModal}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              form="create-project-form"
              isLoading={isSaving}
              loadingLabel="Creating project"
            >
              Create project
            </Button>
          </>
        }
      >
        <form
          className="project-form"
          id="create-project-form"
          onSubmit={(event) => void handleCreate(event)}
        >
          <Input
            label="Project name"
            placeholder="e.g. TeamPulse MVP"
            maxLength={160}
            value={name}
            error={nameError}
            onChange={(event) => setName(event.target.value)}
          />
          <Textarea
            label="Description"
            placeholder="What outcome will this project deliver?"
            maxLength={2000}
            value={description}
            helperText={`${description.length}/2000 characters`}
            onChange={(event) => setDescription(event.target.value)}
          />
          {formError && (
            <p className="project-form__error" role="alert">
              {formError}
            </p>
          )}
        </form>
      </Modal>

      <Modal
        isOpen={Boolean(archiveTarget)}
        onClose={() => setArchiveTarget(null)}
        title="Archive project?"
        description="The project will leave active navigation, but its history and data remain available."
        size="sm"
        isPending={isArchiving}
        footer={
          <>
            <Button
              variant="secondary"
              disabled={isArchiving}
              onClick={() => setArchiveTarget(null)}
            >
              Cancel
            </Button>
            <Button
              variant="danger"
              isLoading={isArchiving}
              loadingLabel="Archiving project"
              onClick={() => void confirmArchive()}
            >
              Archive project
            </Button>
          </>
        }
      >
        <p className="project-archive-copy">
          You are archiving <strong>{archiveTarget?.name}</strong>. This does
          not delete project records.
        </p>
      </Modal>
    </div>
  )
}
