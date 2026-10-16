import { createContext } from 'react'

import type {
  ProjectSummary,
  SaveProjectInput,
} from '@/features/projects/types'

export type ProjectStatus = 'idle' | 'loading' | 'ready' | 'error'

export interface ProjectContextValue {
  status: ProjectStatus
  projects: ProjectSummary[]
  currentProject: ProjectSummary | null
  error: string | null
  createProject: (input: SaveProjectInput) => Promise<ProjectSummary>
  updateProject: (
    projectId: string,
    input: SaveProjectInput,
  ) => Promise<ProjectSummary>
  archiveProject: (projectId: string) => Promise<ProjectSummary>
  reload: () => Promise<void>
}

export const ProjectContext = createContext<ProjectContextValue | null>(null)
