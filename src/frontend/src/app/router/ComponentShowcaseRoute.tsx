import { lazy, Suspense } from 'react'

import { RouteLoading } from '@/app/components/RouteLoading'

const ComponentShowcasePage = lazy(
  () => import('@/features/dev/pages/ComponentShowcasePage'),
)

export function ComponentShowcaseRoute() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ComponentShowcasePage />
    </Suspense>
  )
}
