import type {
  AuthErrorBody,
  AuthSession,
  AuthUser,
  LoginInput,
  RegisterInput,
} from '@/features/auth/types'

export class AuthApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly body: AuthErrorBody,
  ) {
    super(body.message)
  }
}

async function parseError(response: Response): Promise<AuthApiError> {
  const fallback: AuthErrorBody = {
    code: 'request_failed',
    message: 'Something went wrong. Please try again.',
  }

  try {
    const body = (await response.json()) as Partial<AuthErrorBody>
    return new AuthApiError(response.status, {
      code: body.code ?? fallback.code,
      message: body.message ?? fallback.message,
      errors: body.errors,
    })
  } catch {
    return new AuthApiError(response.status, fallback)
  }
}

async function request<T>(path: string, init: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      ...(init.body ? { 'Content-Type': 'application/json' } : {}),
      ...init.headers,
    },
  })

  if (!response.ok) {
    throw await parseError(response)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

let refreshRequest: Promise<AuthSession> | undefined

export const authApi = {
  register(input: RegisterInput) {
    return request<AuthSession>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify(input),
    })
  },

  login(input: LoginInput) {
    return request<AuthSession>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify(input),
    })
  },

  refresh() {
    refreshRequest ??= request<AuthSession>('/api/auth/refresh', {
      method: 'POST',
    }).finally(() => {
      refreshRequest = undefined
    })

    return refreshRequest
  },

  async logout() {
    if (refreshRequest) {
      try {
        await refreshRequest
      } catch {
        // Continue logout even when the in-flight refresh has already failed.
      }
    }

    return request<void>('/api/auth/logout', { method: 'POST' })
  },

  getCurrentUser(accessToken: string) {
    return request<AuthUser>('/api/auth/me', {
      method: 'GET',
      headers: { Authorization: `Bearer ${accessToken}` },
    })
  },
}
