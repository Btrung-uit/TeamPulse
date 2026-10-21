import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'

import { Sidebar } from '@/app/components/Sidebar'
import {
  ProjectContext,
  type ProjectContextValue,
} from '@/features/projects/ProjectContext'
import type { ProjectSummary } from '@/features/projects/types'
import type { WorkspaceSummary } from '@/features/workspaces/types'
import {
  WorkspaceContext,
  type WorkspaceContextValue,
} from '@/features/workspaces/WorkspaceContext'

const engineeringWorkspace: WorkspaceSummary = {
  id: 'workspace-1',
  name: 'Software Engineering',
  role: 'Leader',
  memberCount: 4,
}

const databaseWorkspace: WorkspaceSummary = {
  id: 'workspace-2',
  name: 'Database Lab',
  role: 'Member',
  memberCount: 2,
}

const engineeringProject: ProjectSummary = {
  id: 'project-1',
  workspaceId: engineeringWorkspace.id,
  name: 'TeamPulse MVP',
  description: 'Collaboration workflow',
  isArchived: false,
  createdAtUtc: '2026-10-16T00:00:00Z',
  updatedAtUtc: '2026-10-16T00:00:00Z',
  archivedAtUtc: null,
}

const projectContext: ProjectContextValue = {
  status: 'ready',
  projects: [engineeringProject],
  currentProject: engineeringProject,
  error: null,
  createProject: vi.fn(),
  updateProject: vi.fn(),
  archiveProject: vi.fn(),
  reload: vi.fn(),
}

function LocationProbe() {
  const location = useLocation()
  return <output aria-label="Current location">{location.pathname}</output>
}

function WorkspaceSwitcherHarness({
  createWorkspace = vi.fn(),
}: {
  createWorkspace?: WorkspaceContextValue['createWorkspace']
}) {
  const workspaces = [engineeringWorkspace, databaseWorkspace]
  const [currentWorkspace, setCurrentWorkspace] =
    useState<WorkspaceSummary>(engineeringWorkspace)
  const selectWorkspace = vi.fn((workspaceId: string) => {
    const selected = workspaces.find(
      (workspace) => workspace.id === workspaceId,
    )
    if (selected) setCurrentWorkspace(selected)
  })
  const workspaceContext: WorkspaceContextValue = {
    status: 'ready',
    workspaces,
    currentWorkspace,
    error: null,
    createWorkspace,
    selectWorkspace,
    reload: vi.fn(),
  }

  return (
    <WorkspaceContext.Provider value={workspaceContext}>
      <ProjectContext.Provider value={projectContext}>
        <Sidebar />
        <LocationProbe />
      </ProjectContext.Provider>
    </WorkspaceContext.Provider>
  )
}

describe('workspace switcher', () => {
  it('shows every workspace and navigates without retaining the old project path', async () => {
    const user = userEvent.setup()

    render(
      <MemoryRouter
        initialEntries={['/workspaces/workspace-1/projects/project-1/tasks']}
      >
        <WorkspaceSwitcherHarness />
      </MemoryRouter>,
    )

    await user.click(
      screen.getByRole('button', {
        name: 'Current workspace Software Engineering 4 members',
      }),
    )

    expect(
      screen.getByRole('menuitem', {
        name: 'Software Engineering, 4 members, Leader',
      }),
    ).toBeVisible()
    await user.click(
      screen.getByRole('menuitem', {
        name: 'Database Lab, 2 members, Member',
      }),
    )

    expect(screen.getByLabelText('Current location')).toHaveTextContent(
      '/workspaces/workspace-2/projects',
    )
    expect(
      screen.getByRole('button', {
        name: 'Current workspace Database Lab 2 members',
      }),
    ).toBeVisible()
    expect(screen.queryByRole('menu', { name: 'Switch workspace' })).toBeNull()
  })

  it('creates another workspace from the switcher and opens its project list', async () => {
    const user = userEvent.setup()
    const newWorkspace: WorkspaceSummary = {
      id: 'workspace-3',
      name: 'Operating Systems',
      role: 'Leader',
      memberCount: 1,
    }
    const createWorkspace = vi.fn().mockResolvedValue(newWorkspace)

    render(
      <MemoryRouter initialEntries={['/workspaces/workspace-1/projects']}>
        <WorkspaceSwitcherHarness createWorkspace={createWorkspace} />
      </MemoryRouter>,
    )

    await user.click(
      screen.getByRole('button', {
        name: 'Current workspace Software Engineering 4 members',
      }),
    )
    await user.click(
      screen.getByRole('menuitem', { name: 'Create new workspace' }),
    )
    await user.type(
      screen.getByRole('textbox', { name: 'Workspace name' }),
      'Operating Systems',
    )
    await user.click(screen.getByRole('button', { name: 'Create workspace' }))

    expect(createWorkspace).toHaveBeenCalledWith('Operating Systems')
    await waitFor(() =>
      expect(screen.getByLabelText('Current location')).toHaveTextContent(
        '/workspaces/workspace-3/projects',
      ),
    )
  })
})
