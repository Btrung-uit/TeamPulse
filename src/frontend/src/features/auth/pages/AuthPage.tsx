import { useState, type FormEvent } from 'react'
import { ArrowRight, CheckCircle2, ShieldCheck } from 'lucide-react'
import { Link, useLocation, useNavigate } from 'react-router-dom'

import { Button } from '@/components/ui/Button/Button'
import { Input } from '@/components/ui/Input/Input'
import { AuthApiError } from '@/features/auth/authApi'
import { useAuth } from '@/features/auth/useAuth'

interface AuthPageProps {
  mode: 'login' | 'register'
}

interface LocationState {
  from?: string
}

export function AuthPage({ mode }: AuthPageProps) {
  const isRegister = mode === 'register'
  const { login, register } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFieldErrors({})
    setFormError(null)

    if (isRegister && password !== confirmPassword) {
      setFieldErrors({ confirmPassword: ['Passwords do not match.'] })
      return
    }

    setIsSubmitting(true)

    try {
      if (isRegister) {
        await register({ displayName, email, password })
      } else {
        await login({ email, password })
      }

      const state = location.state as LocationState | null
      navigate(state?.from ?? '/', { replace: true })
    } catch (error) {
      if (error instanceof AuthApiError) {
        setFieldErrors(error.body.errors ?? {})
        setFormError(error.body.message)
      } else {
        setFormError('Unable to reach TeamPulse. Please try again.')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="auth-page">
      <section className="auth-page__brand" aria-label="TeamPulse introduction">
        <div className="auth-page__brand-content">
          <Link
            className="auth-page__logo"
            to="/login"
            aria-label="TeamPulse home"
          >
            <span className="auth-page__logo-mark" aria-hidden="true">
              TP
            </span>
            TeamPulse
          </Link>

          <div className="auth-page__story">
            <p className="eyebrow">Project clarity</p>
            <h1>Turn teamwork into visible progress.</h1>
            <p>
              Plan tasks, connect evidence, and present every contribution from
              one calm workspace.
            </p>
          </div>

          <ul className="auth-page__benefits">
            <li>
              <CheckCircle2 aria-hidden="true" size={18} />
              One source of truth for project progress
            </li>
            <li>
              <CheckCircle2 aria-hidden="true" size={18} />
              Evidence-aware collaboration and review
            </li>
            <li>
              <CheckCircle2 aria-hidden="true" size={18} />
              Clear contribution reports for every member
            </li>
          </ul>
        </div>
      </section>

      <section className="auth-page__form-panel">
        <div className="auth-page__form-wrap">
          <div className="auth-page__mobile-logo" aria-hidden="true">
            TP
          </div>
          <p className="eyebrow">
            {isRegister ? 'Create account' : 'Welcome back'}
          </p>
          <h1>
            {isRegister ? 'Start your workspace' : 'Sign in to TeamPulse'}
          </h1>
          <p className="auth-page__subtitle">
            {isRegister
              ? 'Create your profile first. Workspace setup comes next.'
              : 'Continue where your team left off.'}
          </p>

          <form className="auth-page__form" onSubmit={handleSubmit} noValidate>
            {isRegister && (
              <Input
                label="Display name"
                name="displayName"
                autoComplete="name"
                value={displayName}
                onChange={(event) => setDisplayName(event.target.value)}
                error={fieldErrors.displayName?.[0]}
                required
              />
            )}

            <Input
              label="Email"
              name="email"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              error={fieldErrors.email?.[0]}
              required
            />

            <Input
              label="Password"
              name="password"
              type="password"
              autoComplete={isRegister ? 'new-password' : 'current-password'}
              helperText={
                isRegister
                  ? 'At least 8 characters with uppercase, lowercase, and a number.'
                  : undefined
              }
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              error={fieldErrors.password?.[0]}
              required
            />

            {isRegister && (
              <Input
                label="Confirm password"
                name="confirmPassword"
                type="password"
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                error={fieldErrors.confirmPassword?.[0]}
                required
              />
            )}

            {formError && (
              <div className="auth-page__error" role="alert">
                <ShieldCheck aria-hidden="true" size={18} />
                <span>{formError}</span>
              </div>
            )}

            <Button
              className="auth-page__submit"
              type="submit"
              size="lg"
              isLoading={isSubmitting}
              loadingLabel={isRegister ? 'Creating account' : 'Signing in'}
              trailingIcon={<ArrowRight size={18} />}
            >
              {isRegister ? 'Create account' : 'Sign in'}
            </Button>
          </form>

          <p className="auth-page__switch">
            {isRegister ? 'Already have an account?' : 'New to TeamPulse?'}{' '}
            <Link to={isRegister ? '/login' : '/register'}>
              {isRegister ? 'Sign in' : 'Create account'}
            </Link>
          </p>

          <p className="auth-page__security-note">
            Your account is protected with a secure, private session.
          </p>
        </div>
      </section>
    </main>
  )
}

export default AuthPage
