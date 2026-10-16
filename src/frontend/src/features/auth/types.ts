export interface AuthUser {
  id: string
  email: string
  displayName: string
}

export interface AuthSession {
  user: AuthUser
  accessToken: string
  accessTokenExpiresAtUtc: string
}

export interface AuthErrorBody {
  code: string
  message: string
  errors?: Record<string, string[]>
}

export interface LoginInput {
  email: string
  password: string
}

export interface RegisterInput extends LoginInput {
  displayName: string
}
