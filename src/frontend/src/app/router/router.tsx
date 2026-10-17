import { createBrowserRouter, Navigate } from 'react-router-dom'

import { RouteErrorBoundary } from '@/app/errors/RouteErrorBoundary'
import { AppShell } from '@/app/layouts/AppShell'
import { ComponentShowcaseRoute } from '@/app/router/ComponentShowcaseRoute'
import { DashboardRoute } from '@/app/router/DashboardRoute'
import { ProtectedRoute, PublicOnlyRoute } from '@/features/auth/ProtectedRoute'
import { AuthPage } from '@/features/auth/pages/AuthPage'
import { ProjectListPage } from '@/features/projects/pages/ProjectListPage'
import {
  ProjectProviderRoute,
  ProjectRequiredRoute,
} from '@/features/projects/ProjectRoutes'
import { NotFoundPage } from '@/features/shell/pages/NotFoundPage'
import { MobileMorePage } from '@/features/shell/pages/MobileMorePage'
import { PlaceholderPage } from '@/features/shell/pages/PlaceholderPage'
import { TaskBoardPage } from '@/features/tasks/pages/TaskBoardPage'
import { MyTasksPage } from '@/features/tasks/pages/MyTasksPage'
import { WorkspaceOnboardingPage } from '@/features/workspaces/pages/WorkspaceOnboardingPage'
import {
  WorkspaceOnboardingRoute,
  WorkspaceHomeRedirect,
  WorkspaceRequiredRoute,
  WorkspaceUrlRoute,
} from '@/features/workspaces/WorkspaceRoutes'

export const router = createBrowserRouter([
  {
    element: <PublicOnlyRoute />,
    errorElement: <RouteErrorBoundary />,
    children: [
      {
        path: '/login',
        element: <AuthPage mode="login" />,
      },
      {
        path: '/register',
        element: <AuthPage mode="register" />,
      },
    ],
  },
  {
    element: <ProtectedRoute />,
    errorElement: <RouteErrorBoundary />,
    children: [
      {
        element: <WorkspaceOnboardingRoute />,
        children: [
          {
            path: '/onboarding/workspace',
            element: <WorkspaceOnboardingPage />,
          },
        ],
      },
      {
        element: <WorkspaceRequiredRoute />,
        children: [
          {
            index: true,
            element: <WorkspaceHomeRedirect />,
          },
          {
            path: '/workspaces/:workspaceId',
            element: <WorkspaceUrlRoute />,
            children: [
              {
                element: <ProjectProviderRoute />,
                children: [
                  {
                    element: <AppShell />,
                    children: [
                      {
                        index: true,
                        element: <Navigate to="projects" replace />,
                      },
                      { path: 'projects', element: <ProjectListPage /> },
                      {
                        path: 'projects/:projectId',
                        element: <ProjectRequiredRoute />,
                        children: [
                          { index: true, element: <DashboardRoute /> },
                          {
                            path: 'tasks',
                            element: <TaskBoardPage />,
                          },
                          {
                            path: 'import',
                            element: (
                              <PlaceholderPage
                                eyebrow="Excel onboarding"
                                title="Import tasks"
                                description="Upload, column mapping and validation preview will be delivered in the Excel import milestone."
                              />
                            ),
                          },
                          {
                            path: 'my-tasks',
                            element: <MyTasksPage />,
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
                            element: <MobileMorePage />,
                          },
                        ],
                      },
                      {
                        path: 'dev/components',
                        element: <ComponentShowcaseRoute />,
                      },
                      { path: '*', element: <NotFoundPage /> },
                    ],
                  },
                ],
              },
            ],
          },
        ],
      },
    ],
  },
])
