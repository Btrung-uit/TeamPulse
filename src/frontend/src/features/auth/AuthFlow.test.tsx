import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'

import { AuthApiError } from '@/features/auth/authApi'
import { AuthContext, type AuthContextValue } from '@/features/auth/AuthContext'
import { AuthPage } from '@/features/auth/pages/AuthPage'
import { ProtectedRoute } from '@/features/auth/ProtectedRoute'

function renderWithAuth(
  value: AuthContextValue,
  routes: Parameters<typeof createMemoryRouter>[0],
  initialEntry: string,
) {
  const router = createMemoryRouter(routes, { initialEntries: [initialEntry] })

  return render(
    <AuthContext.Provider value={value}>
      <RouterProvider router={router} />
    </AuthContext.Provider>,
  )
}

function anonymousAuth(
  overrides: Partial<AuthContextValue> = {},
): AuthContextValue {
  return {
    status: 'anonymous',
    user: null,
    accessToken: null,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    ...overrides,
  }
}

describe('authentication flow', () => {
  it('redirects anonymous visitors from protected content to login', async () => {
    renderWithAuth(
      anonymousAuth(),
      [
        {
          element: <ProtectedRoute />,
          children: [{ path: '/', element: <div>Private dashboard</div> }],
        },
        { path: '/login', element: <div>Login destination</div> },
      ],
      '/',
    )

    expect(await screen.findByText('Login destination')).toBeInTheDocument()
    expect(screen.queryByText('Private dashboard')).not.toBeInTheDocument()
  })

  it('shows the safe API error when login fails', async () => {
    const user = userEvent.setup()
    const login = vi.fn().mockRejectedValue(
      new AuthApiError(401, {
        code: 'invalid_credentials',
        message: 'Invalid email or password.',
      }),
    )

    renderWithAuth(
      anonymousAuth({ login }),
      [{ path: '/login', element: <AuthPage mode="login" /> }],
      '/login',
    )

    await user.type(screen.getByLabelText('Email'), 'member@example.com')
    await user.type(screen.getByLabelText('Password'), 'WrongPass123')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Invalid email or password.',
    )
    expect(login).toHaveBeenCalledWith({
      email: 'member@example.com',
      password: 'WrongPass123',
    })
  })
})
