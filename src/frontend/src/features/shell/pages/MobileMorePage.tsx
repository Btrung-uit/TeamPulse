import {
  FolderOpen,
  GitPullRequest,
  LogOut,
  Moon,
  Presentation,
  Sun,
  UserRound,
} from 'lucide-react'
import { Link } from 'react-router-dom'

import { useTheme } from '@/app/providers/useTheme'
import { Button } from '@/components/ui/Button/Button'
import { useAuth } from '@/features/auth/useAuth'
import { useProject } from '@/features/projects/useProject'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

export function MobileMorePage() {
  const { theme, toggleTheme } = useTheme()
  const { user, logout } = useAuth()
  const { currentWorkspace } = useWorkspace()
  const { currentProject } = useProject()
  const projectBase = `/workspaces/${currentWorkspace!.id}/projects/${currentProject!.id}`
  const nextTheme = theme === 'dark' ? 'light' : 'dark'

  return (
    <div className="mobile-more-page">
      <header>
        <p className="eyebrow">Mobile monitoring</p>
        <h1>More</h1>
        <p>
          Open secondary project views without exposing planning or admin tools.
        </p>
      </header>

      <section className="mobile-more-profile" aria-label="Signed-in profile">
        <span aria-hidden="true">
          <UserRound size={20} />
        </span>
        <div>
          <strong>{user!.displayName}</strong>
          <small>{user!.email}</small>
        </div>
      </section>

      <nav className="mobile-more-links" aria-label="More project destinations">
        <Link to={`${projectBase}/engineering/pull-requests`}>
          <GitPullRequest aria-hidden="true" size={19} />
          <span>
            <strong>PR status</strong>
            <small>See work waiting for review</small>
          </span>
        </Link>
        <Link to={`${projectBase}/resources`}>
          <FolderOpen aria-hidden="true" size={19} />
          <span>
            <strong>Resources</strong>
            <small>Open project links and documents</small>
          </span>
        </Link>
        <Link to={`${projectBase}/presentation`}>
          <Presentation aria-hidden="true" size={19} />
          <span>
            <strong>Presentation</strong>
            <small>Review the project story</small>
          </span>
        </Link>
      </nav>

      <section className="mobile-more-settings" aria-label="Personal settings">
        <Button
          variant="secondary"
          leadingIcon={
            theme === 'dark' ? <Sun size={18} /> : <Moon size={18} />
          }
          onClick={toggleTheme}
        >
          Switch to {nextTheme} theme
        </Button>
        <Button
          variant="ghost"
          leadingIcon={<LogOut size={18} />}
          onClick={() => void logout()}
        >
          Sign out
        </Button>
      </section>
    </div>
  )
}
