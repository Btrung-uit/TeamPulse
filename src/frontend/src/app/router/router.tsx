import { createBrowserRouter, Navigate } from 'react-router-dom'

import { RouteErrorBoundary } from '@/app/errors/RouteErrorBoundary'
import { AppShell } from '@/app/layouts/AppShell'
import { ComponentShowcaseRoute } from '@/app/router/ComponentShowcaseRoute'
import { DashboardRoute } from '@/app/router/DashboardRoute'
import { PresentationRoute } from '@/app/router/PresentationRoute'
import { ProtectedRoute, PublicOnlyRoute } from '@/features/auth/ProtectedRoute'
import { AuthPage } from '@/features/auth/pages/AuthPage'
import { HelpPage } from '@/features/help/pages/HelpPage'
import { ProjectListPage } from '@/features/projects/pages/ProjectListPage'
import { PullRequestWaitingRoomPage } from '@/features/pullRequests/pages/PullRequestWaitingRoomPage'
import { ResourceHubPage } from '@/features/resources/pages/ResourceHubPage'
import {
  ProjectProviderRoute,
  ProjectRequiredRoute,
} from '@/features/projects/ProjectRoutes'
import { NotFoundPage } from '@/features/shell/pages/NotFoundPage'
import { MobileMorePage } from '@/features/shell/pages/MobileMorePage'
import { NotificationsPage } from '@/features/shell/pages/NotificationsPage'
import { TaskBoardPage } from '@/features/tasks/pages/TaskBoardPage'
import { MyTasksPage } from '@/features/tasks/pages/MyTasksPage'
import { TaskImportPage } from '@/features/tasks/pages/TaskImportPage'
import { WorkspaceOnboardingPage } from '@/features/workspaces/pages/WorkspaceOnboardingPage'
import { WorkspaceMembersPage } from '@/features/workspaces/pages/WorkspaceMembersPage'
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
                    path: 'projects/:projectId/presentation',
                    element: <ProjectRequiredRoute />,
                    children: [{ index: true, element: <PresentationRoute /> }],
                  },
                  {
                    element: <AppShell />,
                    children: [
                      {
                        index: true,
                        element: <Navigate to="projects" replace />,
                      },
                      { path: 'projects', element: <ProjectListPage /> },
                      { path: 'members', element: <WorkspaceMembersPage /> },
                      { path: 'help', element: <HelpPage /> },
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
                            element: <TaskImportPage />,
                          },
                          {
                            path: 'my-tasks',
                            element: <MyTasksPage />,
                          },
                          {
                            path: 'engineering/pull-requests',
                            element: <PullRequestWaitingRoomPage />,
                          },
                          {
                            path: 'resources',
                            element: <ResourceHubPage />,
                          },
                          {
                            path: 'inbox',
                            element: <NotificationsPage />,
                          },
                          {
                            path: 'more',
                            element: <MobileMorePage />,
                          },
                        ],
                      },
                      ...(import.meta.env.DEV
                        ? [
                            {
                              path: 'dev/components',
                              element: <ComponentShowcaseRoute />,
                            },
                          ]
                        : []),
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
