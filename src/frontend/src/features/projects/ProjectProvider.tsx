import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren,
} from 'react'
import { useParams } from 'react-router-dom'

import { useAuth } from '@/features/auth/useAuth'
import { ProjectContext } from '@/features/projects/ProjectContext'
import { projectApi } from '@/features/projects/projectApi'
import type {
  ProjectSummary,
  SaveProjectInput,
} from '@/features/projects/types'

export function ProjectProvider({ children }: PropsWithChildren) {
  const { accessToken } = useAuth()
  const { workspaceId, projectId } = useParams()
  const [status, setStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>(
    'idle',
  )
  const [projects, setProjects] = useState<ProjectSummary[]>([])
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    if (!accessToken || !workspaceId) {
      setProjects([])
      setStatus('idle')
      return
    }

    setStatus('loading')
    setError(null)
    try {
      setProjects(await projectApi.list(accessToken, workspaceId))
      setStatus('ready')
    } catch {
      setError('Unable to load projects. Check the API and try again.')
      setStatus('error')
    }
  }, [accessToken, workspaceId])

  useEffect(() => {
    void load()
  }, [load])

  const currentProject = useMemo(
    () => projects.find((project) => project.id === projectId) ?? null,
    [projectId, projects],
  )

  async function createProject(input: SaveProjectInput) {
    if (!accessToken || !workspaceId) throw new Error('Workspace is required.')
    const project = await projectApi.create(accessToken, workspaceId, input)
    setProjects((current) => [project, ...current])
    return project
  }

  async function updateProject(
    projectIdToUpdate: string,
    input: SaveProjectInput,
  ) {
    if (!accessToken || !workspaceId) throw new Error('Workspace is required.')
    const project = await projectApi.update(
      accessToken,
      workspaceId,
      projectIdToUpdate,
      input,
    )
    setProjects((current) =>
      current.map((candidate) =>
        candidate.id === project.id ? project : candidate,
      ),
    )
    return project
  }

  async function archiveProject(projectIdToArchive: string) {
    if (!accessToken || !workspaceId) throw new Error('Workspace is required.')
    const project = await projectApi.archive(
      accessToken,
      workspaceId,
      projectIdToArchive,
    )
    setProjects((current) =>
      current.map((candidate) =>
        candidate.id === project.id ? project : candidate,
      ),
    )
    return project
  }

  return (
    <ProjectContext.Provider
      value={{
        status,
        projects,
        currentProject,
        error,
        createProject,
        updateProject,
        archiveProject,
        reload: load,
      }}
    >
      {children}
    </ProjectContext.Provider>
  )
}
