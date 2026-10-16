import { useState, type FormEvent } from 'react'
import { ArrowRight, Check, LogOut, UsersRound } from 'lucide-react'
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
  const { createWorkspace } = useWorkspace()
  const [name, setName] = useState('')
  const [fieldError, setFieldError] = useState<string | undefined>()
  const [formError, setFormError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

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
        <h1>Create your first workspace</h1>
        <p className="workspace-onboarding__intro">
          Welcome, {user?.displayName}. Give your team a clear home. You can add
          registered members and projects next.
        </p>

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

        <p className="workspace-onboarding__note">
          Project details and integrations are intentionally configured later.
        </p>
      </section>
    </main>
  )
}
