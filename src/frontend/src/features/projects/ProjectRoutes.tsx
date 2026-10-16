import { FolderKanban } from 'lucide-react'
import { Link, Outlet, useParams } from 'react-router-dom'

import { RouteLoading } from '@/app/components/RouteLoading'
import { Button } from '@/components/ui/Button/Button'
import { EmptyState } from '@/components/ui/EmptyState/EmptyState'
import { ProjectProvider } from '@/features/projects/ProjectProvider'
import { projectListPath } from '@/features/projects/projectPaths'
import { useProject } from '@/features/projects/useProject'

export function ProjectProviderRoute() {
  return (
    <ProjectProvider>
      <Outlet />
    </ProjectProvider>
  )
}

export function ProjectRequiredRoute() {
  const { workspaceId = '', projectId } = useParams()
  const { status, currentProject, error, reload } = useProject()

  if (status === 'idle' || status === 'loading') return <RouteLoading />

  if (status === 'error') {
    return (
      <section className="project-route-state" role="alert">
        <p className="eyebrow">Connection issue</p>
        <h1>We could not load this project.</h1>
        <p>{error}</p>
        <Button onClick={() => void reload()}>Try again</Button>
      </section>
    )
  }

  if (!projectId || !currentProject) {
    return (
      <EmptyState
        icon={<FolderKanban size={24} />}
        title="Project not found"
        description="This project does not belong to the selected workspace, was removed, or the link is invalid."
        variant="permission"
        primaryAction={
          <Link
            className="ui-button ui-button--primary ui-button--md"
            to={projectListPath(workspaceId)}
          >
            View projects
          </Link>
        }
      />
    )
  }

  return <Outlet />
}
