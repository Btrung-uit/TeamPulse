import { lazy, Suspense } from 'react'

import { RouteLoading } from '@/app/components/RouteLoading'

const DashboardPage = lazy(
  () => import('@/features/dashboard/pages/DashboardPage'),
)

export function DashboardRoute() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <DashboardPage />
    </Suspense>
  )
}
