import { lazy, Suspense } from 'react'

import { RouteLoading } from '@/app/components/RouteLoading'

const PresentationPage = lazy(
  () => import('@/features/presentation/pages/PresentationPage'),
)

export function PresentationRoute() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <PresentationPage />
    </Suspense>
  )
}
