import { useCallback, useEffect, useState, type PropsWithChildren } from 'react'

import { useAuth } from '@/features/auth/useAuth'
import type { WorkspaceSummary } from '@/features/workspaces/types'
import {
  WorkspaceContext,
  type WorkspaceStatus,
} from '@/features/workspaces/WorkspaceContext'
import { workspaceApi } from '@/features/workspaces/workspaceApi'

export function WorkspaceProvider({ children }: PropsWithChildren) {
  const { accessToken } = useAuth()
  const [status, setStatus] = useState<WorkspaceStatus>('idle')
  const [workspaces, setWorkspaces] = useState<WorkspaceSummary[]>([])
  const [currentWorkspace, setCurrentWorkspace] =
    useState<WorkspaceSummary | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    if (!accessToken) {
      setWorkspaces([])
      setCurrentWorkspace(null)
      setError(null)
      setStatus('idle')
      return
    }

    setStatus('loading')
    setError(null)

    try {
      const loadedWorkspaces = await workspaceApi.list(accessToken)
      setWorkspaces(loadedWorkspaces)
      setCurrentWorkspace(
        (current) =>
          loadedWorkspaces.find((workspace) => workspace.id === current?.id) ??
          loadedWorkspaces[0] ??
          null,
      )
      setStatus('ready')
    } catch {
      setError('Unable to load your workspaces. Check the API and try again.')
      setStatus('error')
    }
  }, [accessToken])

  useEffect(() => {
    let active = true

    if (!accessToken) {
      setWorkspaces([])
      setCurrentWorkspace(null)
      setError(null)
      setStatus('idle')
      return
    }

    setStatus('loading')
    setError(null)
    workspaceApi
      .list(accessToken)
      .then((loadedWorkspaces) => {
        if (!active) return
        setWorkspaces(loadedWorkspaces)
        setCurrentWorkspace(
          (current) =>
            loadedWorkspaces.find(
              (workspace) => workspace.id === current?.id,
            ) ??
            loadedWorkspaces[0] ??
            null,
        )
        setStatus('ready')
      })
      .catch(() => {
        if (!active) return
        setError('Unable to load your workspaces. Check the API and try again.')
        setStatus('error')
      })

    return () => {
      active = false
    }
  }, [accessToken])

  async function createWorkspace(name: string) {
    if (!accessToken) {
      throw new Error('An authenticated session is required.')
    }

    const workspace = await workspaceApi.create(accessToken, name)
    setWorkspaces((current) => [...current, workspace])
    setCurrentWorkspace(workspace)
    setStatus('ready')
    return workspace
  }

  function selectWorkspace(workspaceId: string) {
    setCurrentWorkspace(
      workspaces.find((workspace) => workspace.id === workspaceId) ?? null,
    )
  }

  return (
    <WorkspaceContext.Provider
      value={{
        status,
        workspaces,
        currentWorkspace,
        error,
        createWorkspace,
        selectWorkspace,
        reload: load,
      }}
    >
      {children}
    </WorkspaceContext.Provider>
  )
}
