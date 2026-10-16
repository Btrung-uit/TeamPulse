import {
  ClipboardCheck,
  FolderOpen,
  GitPullRequest,
  Inbox,
  LayoutDashboard,
  ListChecks,
  MoreHorizontal,
  Presentation,
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

export const desktopNavigation: NavigationGroup[] = [
  {
    label: 'Overview',
    items: [{ label: 'Dashboard', to: '/', icon: LayoutDashboard, end: true }],
  },
  {
    label: 'Work',
    items: [
      { label: 'Task board', to: '/tasks', icon: ListChecks },
      { label: 'My tasks', to: '/my-tasks', icon: ClipboardCheck },
    ],
  },
  {
    label: 'Engineering',
    items: [
      {
        label: 'PR waiting room',
        to: '/engineering/pull-requests',
        icon: GitPullRequest,
      },
    ],
  },
  {
    label: 'Resources',
    items: [{ label: 'Resource hub', to: '/resources', icon: FolderOpen }],
  },
  {
    label: 'Presentation',
    items: [
      {
        label: 'Presentation mode',
        to: '/presentation',
        icon: Presentation,
      },
    ],
  },
]

export const mobileNavigation: NavigationItem[] = [
  { label: 'Home', to: '/', icon: LayoutDashboard, end: true },
  { label: 'Tasks', to: '/my-tasks', icon: ClipboardCheck },
  { label: 'Inbox', to: '/inbox', icon: Inbox },
  { label: 'More', to: '/more', icon: MoreHorizontal },
]
