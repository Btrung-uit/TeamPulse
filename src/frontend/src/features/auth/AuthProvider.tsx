import { useEffect, useState, type PropsWithChildren } from 'react'

import { authApi } from '@/features/auth/authApi'
import {
  AuthContext,
  type AuthContextValue,
  type AuthStatus,
} from '@/features/auth/AuthContext'
import type {
  AuthSession,
  LoginInput,
  RegisterInput,
} from '@/features/auth/types'

const refreshLeadTimeMs = 60_000

export function AuthProvider({ children }: PropsWithChildren) {
  const [status, setStatus] = useState<AuthStatus>('initializing')
  const [session, setSession] = useState<AuthSession | null>(null)

  useEffect(() => {
    let active = true

    authApi
      .refresh()
      .then((restoredSession) => {
        if (active) {
          setSession(restoredSession)
          setStatus('authenticated')
        }
      })
      .catch(() => {
        if (active) {
          setSession(null)
          setStatus('anonymous')
        }
      })

    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    if (!session) {
      return
    }

    let active = true
    const expiresAt = Date.parse(session.accessTokenExpiresAtUtc)
    const delay = Math.max(expiresAt - Date.now() - refreshLeadTimeMs, 0)
    const timer = window.setTimeout(() => {
      authApi
        .refresh()
        .then((refreshedSession) => {
          if (active) {
            setSession(refreshedSession)
            setStatus('authenticated')
          }
        })
        .catch(() => {
          if (active) {
            setSession(null)
            setStatus('anonymous')
          }
        })
    }, delay)

    return () => {
      active = false
      window.clearTimeout(timer)
    }
  }, [session])

  async function login(input: LoginInput) {
    const session = await authApi.login(input)
    setSession(session)
    setStatus('authenticated')
    return session
  }

  async function register(input: RegisterInput) {
    const session = await authApi.register(input)
    setSession(session)
    setStatus('authenticated')
    return session
  }

  async function logout() {
    setSession(null)
    setStatus('anonymous')
    try {
      await authApi.logout()
    } catch {
      // Local session is cleared even if the server is temporarily unavailable.
    }
  }

  const value: AuthContextValue = {
    status,
    user: session?.user ?? null,
    accessToken: session?.accessToken ?? null,
    login,
    register,
    logout,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
