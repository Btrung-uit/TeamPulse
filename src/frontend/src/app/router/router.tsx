import { createBrowserRouter } from 'react-router-dom'

import { RouteErrorBoundary } from '@/app/errors/RouteErrorBoundary'
import { AppShell } from '@/app/layouts/AppShell'
import { ComponentShowcaseRoute } from '@/app/router/ComponentShowcaseRoute'
import { DashboardRoute } from '@/app/router/DashboardRoute'
import { NotFoundPage } from '@/features/shell/pages/NotFoundPage'
import { PlaceholderPage } from '@/features/shell/pages/PlaceholderPage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    errorElement: <RouteErrorBoundary />,
    children: [
      {
        index: true,
        element: <DashboardRoute />,
      },
      {
        path: 'tasks',
        element: (
          <PlaceholderPage
            eyebrow="Work"
            title="Task board"
            description="Kanban planning, filters and task creation will be delivered in the task-management milestone."
          />
        ),
      },
      {
        path: 'my-tasks',
        element: (
          <PlaceholderPage
            eyebrow="Work"
            title="My tasks"
            description="Your focused task queue will appear here after workspace membership and task lifecycle are implemented."
          />
        ),
      },
      {
        path: 'engineering/pull-requests',
        element: (
          <PlaceholderPage
            eyebrow="Engineering"
            title="PR waiting room"
            description="Pull requests awaiting review will be connected during the GitHub integration phase."
          />
        ),
      },
      {
        path: 'resources',
        element: (
          <PlaceholderPage
            eyebrow="Resources"
            title="Resource hub"
            description="Project documents, repositories, demos and presentation links will be organized here."
          />
        ),
      },
      {
        path: 'presentation',
        element: (
          <PlaceholderPage
            eyebrow="Presentation"
            title="Presentation mode"
            description="A clean, read-only story of progress and contribution will be built in a later milestone."
          />
        ),
      },
      {
        path: 'inbox',
        element: (
          <PlaceholderPage
            eyebrow="Inbox"
            title="Notifications"
            description="Review requests, deadline risks and mentions will be collected here."
          />
        ),
      },
      {
        path: 'more',
        element: (
          <PlaceholderPage
            eyebrow="Mobile"
            title="More"
            description="Secondary mobile destinations will be grouped here without exposing high-risk admin actions."
          />
        ),
      },
      {
        path: 'dev/components',
        element: <ComponentShowcaseRoute />,
      },
      {
        path: '*',
        element: <NotFoundPage />,
      },
    ],
  },
])
