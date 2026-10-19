import { useEffect, useState, type FormEvent } from 'react'
import { ShieldCheck, UserPlus, Users } from 'lucide-react'

import { Button } from '@/components/ui/Button/Button'
import { EmptyState } from '@/components/ui/EmptyState/EmptyState'
import { Input } from '@/components/ui/Input/Input'
import { useAuth } from '@/features/auth/useAuth'
import type {
  WorkspaceMember,
  WorkspaceRole,
} from '@/features/workspaces/types'
import { useWorkspace } from '@/features/workspaces/useWorkspace'
import {
  WorkspaceApiError,
  workspaceApi,
} from '@/features/workspaces/workspaceApi'

const roles: WorkspaceRole[] = ['Member', 'Advisor', 'Leader']

export function WorkspaceMembersPage() {
  const { accessToken } = useAuth()
  const { currentWorkspace, reload: reloadWorkspaces } = useWorkspace()
  const [members, setMembers] = useState<WorkspaceMember[]>([])
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>('loading')
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<WorkspaceRole>('Member')
  const [emailError, setEmailError] = useState<string>()
  const [formMessage, setFormMessage] = useState<string>()
  const [isSaving, setIsSaving] = useState(false)
  const canManage = currentWorkspace?.role === 'Leader'

  async function loadMembers() {
    if (!accessToken || !currentWorkspace) return
    setStatus('loading')
    try {
      setMembers(
        await workspaceApi.listMembers(accessToken, currentWorkspace.id),
      )
      setStatus('ready')
    } catch {
      setStatus('error')
    }
  }

  useEffect(() => {
    void loadMembers()
    // Reload when the authenticated workspace changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken, currentWorkspace?.id])

  async function handleAddMember(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!accessToken || !currentWorkspace || !canManage) return

    const normalizedEmail = email.trim()
    if (!normalizedEmail) {
      setEmailError('Enter the registered email address.')
      return
    }

    setIsSaving(true)
    setEmailError(undefined)
    setFormMessage(undefined)
    try {
      const member = await workspaceApi.addMember(
        accessToken,
        currentWorkspace.id,
        { email: normalizedEmail, role },
      )
      setMembers((current) =>
        [...current, member].sort((left, right) =>
          left.displayName.localeCompare(right.displayName),
        ),
      )
      setEmail('')
      setRole('Member')
      setFormMessage(`${member.displayName} can now access this workspace.`)
      await reloadWorkspaces()
    } catch (error) {
      if (error instanceof WorkspaceApiError) {
        setEmailError(error.body.errors?.email?.[0] ?? error.body.message)
      } else {
        setEmailError('Unable to add the member. Please try again.')
      }
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <div className="workspace-members-page">
      <header className="workspace-members-header">
        <div>
          <p className="eyebrow">{currentWorkspace?.name}</p>
          <h1>Members</h1>
          <p>
            Manage who can access this workspace and prepare task assignments.
          </p>
        </div>
      </header>

      {canManage && (
        <section
          className="workspace-member-add"
          aria-labelledby="add-member-title"
        >
          <div>
            <p className="eyebrow">Leader action</p>
            <h2 id="add-member-title">Add a registered member</h2>
            <p>
              The teammate must create a TeamPulse account before you add them.
            </p>
          </div>
          <form onSubmit={(event) => void handleAddMember(event)} noValidate>
            <Input
              label="Registered email"
              type="email"
              autoComplete="email"
              placeholder="teammate@example.com"
              value={email}
              error={emailError}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
            <label className="workspace-member-role">
              <span>Role</span>
              <select
                value={role}
                onChange={(event) =>
                  setRole(event.target.value as WorkspaceRole)
                }
              >
                {roles.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </label>
            <Button
              type="submit"
              isLoading={isSaving}
              loadingLabel="Adding member"
              leadingIcon={<UserPlus size={17} />}
            >
              Add member
            </Button>
          </form>
          {formMessage && (
            <p className="workspace-member-add__message" role="status">
              {formMessage}
            </p>
          )}
        </section>
      )}

      <section
        className="workspace-member-list"
        aria-labelledby="member-list-title"
      >
        <div className="workspace-member-list__heading">
          <div>
            <p className="eyebrow">Workspace access</p>
            <h2 id="member-list-title">Current members</h2>
          </div>
          {status === 'ready' && <span>{members.length}</span>}
        </div>

        {status === 'loading' ? (
          <div className="workspace-member-list__loading" role="status">
            Loading members
          </div>
        ) : status === 'error' ? (
          <EmptyState
            icon={<Users size={24} />}
            title="Members could not be loaded"
            description="Check the API connection and try again."
            primaryAction={
              <Button onClick={() => void loadMembers()}>Try again</Button>
            }
          />
        ) : (
          <ul>
            {members.map((member) => (
              <li key={member.userId}>
                <span className="workspace-member-avatar" aria-hidden="true">
                  {member.displayName.charAt(0).toUpperCase()}
                </span>
                <span className="workspace-member-copy">
                  <strong>{member.displayName}</strong>
                  <small>{member.email}</small>
                </span>
                <span className="workspace-member-role-badge">
                  {member.role === 'Leader' && (
                    <ShieldCheck size={15} aria-hidden="true" />
                  )}
                  {member.role}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}
