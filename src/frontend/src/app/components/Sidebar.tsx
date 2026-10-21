import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Check, ChevronDown, Plus } from 'lucide-react'
import { NavLink, useNavigate } from 'react-router-dom'

import { desktopNavigation } from '@/app/navigation/navigation'
import { Button } from '@/components/ui/Button/Button'
import { Input } from '@/components/ui/Input/Input'
import { Modal } from '@/components/ui/Modal/Modal'
import { projectListPath } from '@/features/projects/projectPaths'
import { useProject } from '@/features/projects/useProject'
import { useWorkspace } from '@/features/workspaces/useWorkspace'
import { WorkspaceApiError } from '@/features/workspaces/workspaceApi'

const workspaceNameMaximumLength = 160

export function Sidebar() {
  const { workspaces, currentWorkspace, createWorkspace, selectWorkspace } =
    useWorkspace()
  const { currentProject } = useProject()
  const navigate = useNavigate()
  const [isWorkspaceMenuOpen, setIsWorkspaceMenuOpen] = useState(false)
  const [isCreateWorkspaceOpen, setIsCreateWorkspaceOpen] = useState(false)
  const [workspaceName, setWorkspaceName] = useState('')
  const [workspaceNameError, setWorkspaceNameError] = useState<string>()
  const [workspaceFormError, setWorkspaceFormError] = useState<string | null>(
    null,
  )
  const [isCreatingWorkspace, setIsCreatingWorkspace] = useState(false)
  const workspaceMenuRef = useRef<HTMLDivElement>(null)
  const workspaceNameRef = useRef<HTMLInputElement>(null)
  const initials = currentWorkspace?.name
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase()

  useEffect(() => {
    if (!isWorkspaceMenuOpen) return

    function closeOnOutsideClick(event: MouseEvent) {
      if (!workspaceMenuRef.current?.contains(event.target as Node)) {
        setIsWorkspaceMenuOpen(false)
      }
    }

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') setIsWorkspaceMenuOpen(false)
    }

    document.addEventListener('mousedown', closeOnOutsideClick)
    document.addEventListener('keydown', closeOnEscape)
    return () => {
      document.removeEventListener('mousedown', closeOnOutsideClick)
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [isWorkspaceMenuOpen])

  function switchWorkspace(workspaceId: string) {
    selectWorkspace(workspaceId)
    setIsWorkspaceMenuOpen(false)
    navigate(projectListPath(workspaceId))
  }

  function openCreateWorkspace() {
    setIsWorkspaceMenuOpen(false)
    setWorkspaceName('')
    setWorkspaceNameError(undefined)
    setWorkspaceFormError(null)
    setIsCreateWorkspaceOpen(true)
  }

  async function submitWorkspace(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const normalizedName = workspaceName.trim()

    if (
      normalizedName.length === 0 ||
      normalizedName.length > workspaceNameMaximumLength
    ) {
      setWorkspaceNameError(
        `Workspace name must contain 1 to ${workspaceNameMaximumLength} characters.`,
      )
      return
    }

    setIsCreatingWorkspace(true)
    setWorkspaceNameError(undefined)
    setWorkspaceFormError(null)
    try {
      const workspace = await createWorkspace(normalizedName)
      setIsCreateWorkspaceOpen(false)
      navigate(projectListPath(workspace.id))
    } catch (error) {
      if (error instanceof WorkspaceApiError) {
        setWorkspaceNameError(error.body.errors?.name?.[0])
        setWorkspaceFormError(error.body.message)
      } else {
        setWorkspaceFormError(
          'Unable to create the workspace. Please try again.',
        )
      }
    } finally {
      setIsCreatingWorkspace(false)
    }
  }

  return (
    <aside className="sidebar" aria-label="Primary navigation">
      <NavLink className="brand" to="/">
        <span className="brand__mark" aria-hidden="true">
          TP
        </span>
        <span className="brand__name">TeamPulse</span>
      </NavLink>

      <nav className="sidebar__navigation">
        {desktopNavigation(currentWorkspace!.id, currentProject?.id).map(
          (group) => (
            <div className="navigation-group" key={group.label}>
              <p className="navigation-group__label">{group.label}</p>
              <ul className="navigation-group__list">
                {group.items.map((item) => {
                  const Icon = item.icon

                  return (
                    <li key={item.to}>
                      <NavLink
                        className={({ isActive }) =>
                          `navigation-link${isActive ? ' is-active' : ''}`
                        }
                        end={item.end}
                        to={item.to}
                        title={item.label}
                      >
                        <Icon aria-hidden="true" size={19} strokeWidth={1.8} />
                        <span className="navigation-link__label">
                          {item.label}
                        </span>
                      </NavLink>
                    </li>
                  )
                })}
              </ul>
            </div>
          ),
        )}
      </nav>

      <div className="sidebar__workspace-switcher" ref={workspaceMenuRef}>
        <button
          className="sidebar__workspace"
          type="button"
          aria-haspopup="menu"
          aria-expanded={isWorkspaceMenuOpen}
          aria-label={`Current workspace ${currentWorkspace?.name ?? 'Workspace'}, ${currentWorkspace?.memberCount ?? 0} ${(currentWorkspace?.memberCount ?? 0) === 1 ? 'member' : 'members'}. Switch workspace`}
          title="Switch workspace"
          onClick={() => setIsWorkspaceMenuOpen((open) => !open)}
        >
          <span className="workspace-avatar" aria-hidden="true">
            {initials || 'TP'}
          </span>
          <span className="sidebar__workspace-copy">
            <small className="sidebar__workspace-label">
              Current workspace
            </small>
            <strong>{currentWorkspace?.name ?? 'Workspace'}</strong>
            <small>
              {currentWorkspace?.memberCount ?? 0}{' '}
              {(currentWorkspace?.memberCount ?? 0) === 1
                ? 'member'
                : 'members'}
            </small>
          </span>
          <ChevronDown
            className="sidebar__workspace-chevron"
            aria-hidden="true"
            size={16}
          />
        </button>

        {isWorkspaceMenuOpen && (
          <div
            className="workspace-switcher-menu"
            role="menu"
            aria-label="Switch workspace"
          >
            <p>Your workspaces</p>
            <div className="workspace-switcher-menu__list">
              {workspaces.map((workspace) => {
                const workspaceInitials = workspace.name
                  .split(/\s+/)
                  .slice(0, 2)
                  .map((part) => part[0])
                  .join('')
                  .toUpperCase()
                const memberLabel = `${workspace.memberCount} ${workspace.memberCount === 1 ? 'member' : 'members'}`

                return (
                  <button
                    className="workspace-switcher-menu__item"
                    type="button"
                    role="menuitem"
                    key={workspace.id}
                    aria-label={`${workspace.name}, ${memberLabel}, ${workspace.role}`}
                    onClick={() => switchWorkspace(workspace.id)}
                  >
                    <span
                      className="workspace-switcher-menu__avatar"
                      aria-hidden="true"
                    >
                      {workspaceInitials || 'TP'}
                    </span>
                    <span className="workspace-switcher-menu__copy">
                      <strong>{workspace.name}</strong>
                      <small>
                        {memberLabel} · {workspace.role}
                      </small>
                    </span>
                    {workspace.id === currentWorkspace?.id && (
                      <Check aria-hidden="true" size={16} />
                    )}
                  </button>
                )
              })}
            </div>
            <button
              className="workspace-switcher-menu__create"
              type="button"
              role="menuitem"
              onClick={openCreateWorkspace}
            >
              <Plus aria-hidden="true" size={17} />
              Create new workspace
            </button>
          </div>
        )}
      </div>

      <Modal
        isOpen={isCreateWorkspaceOpen}
        onClose={() => setIsCreateWorkspaceOpen(false)}
        title="Create a new workspace"
        description="Use a separate workspace when the team, course or member list is different."
        size="sm"
        isPending={isCreatingWorkspace}
        initialFocusRef={workspaceNameRef}
        footer={
          <>
            <Button
              variant="secondary"
              disabled={isCreatingWorkspace}
              onClick={() => setIsCreateWorkspaceOpen(false)}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              form="create-workspace-form"
              isLoading={isCreatingWorkspace}
              loadingLabel="Creating workspace"
            >
              Create workspace
            </Button>
          </>
        }
      >
        <form
          id="create-workspace-form"
          onSubmit={(event) => void submitWorkspace(event)}
        >
          <Input
            ref={workspaceNameRef}
            label="Workspace name"
            value={workspaceName}
            maxLength={workspaceNameMaximumLength}
            placeholder="e.g. Database Systems Team"
            helperText="You will become the Leader of this workspace."
            error={workspaceNameError}
            onChange={(event) => {
              setWorkspaceName(event.target.value)
              setWorkspaceNameError(undefined)
              setWorkspaceFormError(null)
            }}
          />
          {workspaceFormError && (
            <p className="workspace-create-error" role="alert">
              {workspaceFormError}
            </p>
          )}
        </form>
      </Modal>
    </aside>
  )
}
