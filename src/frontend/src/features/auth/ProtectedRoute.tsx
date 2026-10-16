import { Navigate, Outlet, useLocation } from 'react-router-dom'

import { RouteLoading } from '@/app/components/RouteLoading'
import { useAuth } from '@/features/auth/useAuth'

export function ProtectedRoute() {
  const { status } = useAuth()
  const location = useLocation()

  if (status === 'initializing') {
    return <RouteLoading />
  }

  if (status === 'anonymous') {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}

export function PublicOnlyRoute() {
  const { status } = useAuth()

  if (status === 'initializing') {
    return <RouteLoading />
  }

  return status === 'authenticated' ? <Navigate to="/" replace /> : <Outlet />
}
