import { createContext } from 'react'

import type {
  AuthSession,
  AuthUser,
  LoginInput,
  RegisterInput,
} from '@/features/auth/types'

export type AuthStatus = 'initializing' | 'authenticated' | 'anonymous'

export interface AuthContextValue {
  status: AuthStatus
  user: AuthUser | null
  accessToken: string | null
  login: (input: LoginInput) => Promise<AuthSession>
  register: (input: RegisterInput) => Promise<AuthSession>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
