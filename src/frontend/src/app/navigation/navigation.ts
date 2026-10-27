import {
  ClipboardCheck,
  FolderOpen,
  GitPullRequest,
  Inbox,
  LayoutDashboard,
  ListChecks,
  MoreHorizontal,
  Palette,
  Presentation,
  Users,
  type LucideIcon,
} from 'lucide-react'

export interface NavigationItem {
  label: string
  to: string
  icon: LucideIcon
  end?: boolean
}

export interface NavigationGroup {
  label: string
  items: NavigationItem[]
}

export function desktopNavigation(
  workspaceId: string,
  projectId?: string,
  includeDevelopment = import.meta.env.DEV,
): NavigationGroup[] {
  const projectBase = projectId
    ? `/workspaces/${workspaceId}/projects/${projectId}`
    : undefined

  return [
    {
      label: 'Overview',
      items: [
        {
          label: 'Projects',
          to: `/workspaces/${workspaceId}/projects`,
          icon: LayoutDashboard,
          end: true,
        },
        {
          label: 'Members',
          to: `/workspaces/${workspaceId}/members`,
          icon: Users,
        },
        ...(projectBase
          ? [
              {
                label: 'Dashboard',
                to: projectBase,
                icon: LayoutDashboard,
                end: true,
              },
            ]
          : []),
      ],
    },
    ...(projectBase
      ? [
          {
            label: 'Work',
            items: [
              {
                label: 'Task board',
                to: `${projectBase}/tasks`,
                icon: ListChecks,
              },
              {
                label: 'My tasks',
                to: `${projectBase}/my-tasks`,
                icon: ClipboardCheck,
              },
            ],
          },
          {
            label: 'Engineering',
            items: [
              {
                label: 'PR waiting room',
                to: `${projectBase}/engineering/pull-requests`,
                icon: GitPullRequest,
              },
            ],
          },
          {
            label: 'Resources',
            items: [
              {
                label: 'Resource hub',
                to: `${projectBase}/resources`,
                icon: FolderOpen,
              },
            ],
          },
          {
            label: 'Presentation',
            items: [
              {
                label: 'Presentation mode',
                to: `${projectBase}/presentation`,
                icon: Presentation,
              },
            ],
          },
        ]
      : []),
    ...(includeDevelopment
      ? [
          {
            label: 'Development',
            items: [
              {
                label: 'UI components',
                to: `/workspaces/${workspaceId}/dev/components`,
                icon: Palette,
              },
            ],
          },
        ]
      : []),
  ]
}

export function mobileNavigation(
  workspaceId: string,
  projectId?: string,
): NavigationItem[] {
  const projectList = `/workspaces/${workspaceId}/projects`
  if (!projectId) {
    return [
      { label: 'Projects', to: projectList, icon: LayoutDashboard, end: true },
    ]
  }

  const projectBase = `${projectList}/${projectId}`

  return [
    { label: 'Home', to: projectBase, icon: LayoutDashboard, end: true },
    { label: 'Tasks', to: `${projectBase}/my-tasks`, icon: ClipboardCheck },
    { label: 'Inbox', to: `${projectBase}/inbox`, icon: Inbox },
    { label: 'More', to: `${projectBase}/more`, icon: MoreHorizontal },
  ]
}
