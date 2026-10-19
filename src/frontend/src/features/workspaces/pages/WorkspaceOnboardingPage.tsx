import { useState, type FormEvent } from 'react'
import {
  ArrowLeft,
  ArrowRight,
  Check,
  Copy,
  LogOut,
  RefreshCw,
  UsersRound,
} from 'lucide-react'
import { useNavigate } from 'react-router-dom'

import { Button } from '@/components/ui/Button/Button'
import { Input } from '@/components/ui/Input/Input'
import { useAuth } from '@/features/auth/useAuth'
import { useWorkspace } from '@/features/workspaces/useWorkspace'
import { WorkspaceApiError } from '@/features/workspaces/workspaceApi'

const workspaceNameMaximumLength = 160

export function WorkspaceOnboardingPage() {
  const navigate = useNavigate()
  const { user, logout } = useAuth()
  const { createWorkspace, reload } = useWorkspace()
  const [mode, setMode] = useState<'create' | 'join'>('create')
  const [name, setName] = useState('')
  const [fieldError, setFieldError] = useState<string | undefined>()
  const [formError, setFormError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isChecking, setIsChecking] = useState(false)
  const [joinMessage, setJoinMessage] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  async function copyEmail() {
    if (!user?.email) return

    try {
      await navigator.clipboard.writeText(user.email)
      setCopied(true)
      setJoinMessage(null)
    } catch {
      setJoinMessage(
        'Unable to copy automatically. Select the email and copy it manually.',
      )
    }
  }

  async function checkWorkspaceAccess() {
    setIsChecking(true)
    setJoinMessage(null)
    try {
      await reload()
      setJoinMessage(
        'No workspace access was found yet. Ask your Leader to add this exact email, then check again.',
      )
    } finally {
      setIsChecking(false)
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const normalizedName = name.trim()
    setFieldError(undefined)
    setFormError(null)

    if (
      normalizedName.length === 0 ||
      normalizedName.length > workspaceNameMaximumLength
    ) {
      setFieldError(
        `Workspace name must contain 1 to ${workspaceNameMaximumLength} characters.`,
      )
      return
    }

    setIsSubmitting(true)
    try {
      await createWorkspace(normalizedName)
      navigate('/', { replace: true })
    } catch (error) {
      if (error instanceof WorkspaceApiError) {
        setFieldError(error.body.errors?.name?.[0])
        setFormError(error.body.message)
      } else {
        setFormError('Unable to create your workspace. Please try again.')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="workspace-onboarding">
      <header className="workspace-onboarding__header">
        <a className="workspace-onboarding__brand" href="/">
          <span aria-hidden="true">TP</span>
          TeamPulse
        </a>
        <Button
          variant="ghost"
          size="sm"
          leadingIcon={<LogOut size={16} />}
          onClick={() => void logout()}
        >
          Sign out
        </Button>
      </header>

      <section className="workspace-onboarding__content">
        <div
          className="workspace-onboarding__progress"
          aria-label="Setup progress"
        >
          <span className="is-complete">
            <Check aria-hidden="true" size={14} />
          </span>
          <i aria-hidden="true" />
          <span aria-current="step">2</span>
          <small>Account</small>
          <small>Workspace</small>
        </div>

        <div className="workspace-onboarding__icon" aria-hidden="true">
          <UsersRound size={28} />
        </div>
        <p className="eyebrow">Your shared space</p>
        <h1>
          {mode === 'create'
            ? 'Create your first workspace'
            : 'Join an existing workspace'}
        </h1>
        <p className="workspace-onboarding__intro">
          {mode === 'create'
            ? `Welcome, ${user?.displayName}. Give your team a clear home. You can add registered members and projects next.`
            : 'A workspace Leader must add your registered email before you can enter the team space.'}
        </p>

        {mode === 'create' ? (
          <>
            <form
              className="workspace-onboarding__form"
              onSubmit={handleSubmit}
              noValidate
            >
              <Input
                label="Workspace name"
                name="workspaceName"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="e.g. Software Engineering Team"
                helperText="Use the name your teammates will recognize."
                error={fieldError}
                maxLength={workspaceNameMaximumLength}
                autoFocus
                required
              />

              {formError && (
                <p className="workspace-onboarding__error" role="alert">
                  {formError}
                </p>
              )}

              <Button
                type="submit"
                size="lg"
                isLoading={isSubmitting}
                loadingLabel="Creating workspace"
                trailingIcon={<ArrowRight size={18} />}
              >
                Create workspace
              </Button>
            </form>
            <div className="workspace-onboarding__alternative">
              <span>Joining someone else's team?</span>
              <Button variant="link" onClick={() => setMode('join')}>
                Join an existing workspace
              </Button>
            </div>
          </>
        ) : (
          <div className="workspace-onboarding__join">
            <div className="workspace-onboarding__email">
              <span>Your registered email</span>
              <strong>{user?.email}</strong>
              <Button
                variant="secondary"
                size="sm"
                leadingIcon={<Copy size={16} />}
                onClick={() => void copyEmail()}
              >
                {copied ? 'Copied' : 'Copy email'}
              </Button>
            </div>
            <ol>
              <li>Send this email to your workspace Leader.</li>
              <li>Ask them to add you from the Members page.</li>
              <li>Return here and check your access.</li>
            </ol>
            {joinMessage && (
              <p className="workspace-onboarding__join-message" role="status">
                {joinMessage}
              </p>
            )}
            <Button
              size="lg"
              isLoading={isChecking}
              loadingLabel="Checking workspace access"
              leadingIcon={<RefreshCw size={17} />}
              onClick={() => void checkWorkspaceAccess()}
            >
              Check workspace access
            </Button>
            <Button
              variant="ghost"
              leadingIcon={<ArrowLeft size={17} />}
              onClick={() => {
                setMode('create')
                setJoinMessage(null)
              }}
            >
              Create a workspace instead
            </Button>
          </div>
        )}

        <p className="workspace-onboarding__note">
          {mode === 'create'
            ? 'Project details and integrations are intentionally configured later.'
            : 'Workspace access is controlled by your team Leader.'}
        </p>
      </section>
    </main>
  )
}
