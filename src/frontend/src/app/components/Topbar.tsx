import { useEffect, useRef, useState } from 'react'
import {
  Bell,
  Check,
  ChevronDown,
  FolderKanban,
  LogOut,
  Moon,
  Search,
  Sun,
} from 'lucide-react'
import { Link } from 'react-router-dom'

import { useTheme } from '@/app/providers/useTheme'
import { useAuth } from '@/features/auth/useAuth'
import { projectListPath, projectPath } from '@/features/projects/projectPaths'
import { useProject } from '@/features/projects/useProject'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

export function Topbar() {
  const { theme, toggleTheme } = useTheme()
  const { user, logout } = useAuth()
  const { currentWorkspace } = useWorkspace()
  const { projects, currentProject, status } = useProject()
  const [isProjectMenuOpen, setIsProjectMenuOpen] = useState(false)
  const projectMenuRef = useRef<HTMLDivElement>(null)
  const nextTheme = theme === 'dark' ? 'light' : 'dark'
  const initials = user?.displayName
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase()
  const activeProjects = projects.filter((project) => !project.isArchived)
  const projectInitial = currentProject?.name.charAt(0).toUpperCase() ?? 'P'

  useEffect(() => {
    if (!isProjectMenuOpen) return

    function closeOnOutsideClick(event: MouseEvent) {
      if (!projectMenuRef.current?.contains(event.target as Node)) {
        setIsProjectMenuOpen(false)
      }
    }

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') setIsProjectMenuOpen(false)
    }

    document.addEventListener('mousedown', closeOnOutsideClick)
    document.addEventListener('keydown', closeOnEscape)
    return () => {
      document.removeEventListener('mousedown', closeOnOutsideClick)
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [isProjectMenuOpen])

  return (
    <header className="topbar">
      <div className="project-switcher-shell" ref={projectMenuRef}>
        <button
          className="project-switcher"
          type="button"
          aria-haspopup="menu"
          aria-expanded={isProjectMenuOpen}
          disabled={status === 'loading'}
          onClick={() => setIsProjectMenuOpen((open) => !open)}
        >
          <span className="project-switcher__mark" aria-hidden="true">
            {projectInitial}
          </span>
          <span className="project-switcher__copy">
            <small>Current project</small>
            <strong>{currentProject?.name ?? 'Select a project'}</strong>
          </span>
          <ChevronDown aria-hidden="true" size={16} />
        </button>

        {isProjectMenuOpen && currentWorkspace && (
          <div className="project-switcher-menu" role="menu">
            <p>Switch project</p>
            {activeProjects.length > 0 ? (
              <div className="project-switcher-menu__list">
                {activeProjects.map((project) => (
                  <Link
                    className="project-switcher-menu__item"
                    key={project.id}
                    role="menuitem"
                    to={projectPath(currentWorkspace.id, project.id)}
                    onClick={() => setIsProjectMenuOpen(false)}
                  >
                    <span aria-hidden="true">
                      {project.name.charAt(0).toUpperCase()}
                    </span>
                    <strong>{project.name}</strong>
                    {project.id === currentProject?.id && (
                      <Check aria-hidden="true" size={16} />
                    )}
                  </Link>
                ))}
              </div>
            ) : (
              <span className="project-switcher-menu__empty">
                No active projects yet.
              </span>
            )}
            <Link
              className="project-switcher-menu__all"
              role="menuitem"
              to={projectListPath(currentWorkspace.id)}
              onClick={() => setIsProjectMenuOpen(false)}
            >
              <FolderKanban aria-hidden="true" size={16} />
              View all projects
            </Link>
          </div>
        )}
      </div>

      <div className="topbar__actions">
        <button className="search-trigger" type="button">
          <Search aria-hidden="true" size={18} />
          <span>Search</span>
          <kbd>⌘ K</kbd>
        </button>

        <button
          className="icon-button"
          type="button"
          aria-label="View notifications, 3 unread"
        >
          <Bell aria-hidden="true" size={19} />
          <span className="notification-dot" aria-hidden="true" />
        </button>

        <button
          className="icon-button"
          type="button"
          onClick={toggleTheme}
          aria-label={`Switch to ${nextTheme} theme`}
        >
          {theme === 'dark' ? (
            <Sun aria-hidden="true" size={19} />
          ) : (
            <Moon aria-hidden="true" size={19} />
          )}
        </button>

        <button
          className="user-menu"
          type="button"
          title="Sign out"
          onClick={() => void logout()}
        >
          <span className="user-menu__avatar" aria-hidden="true">
            {initials || 'TP'}
          </span>
          <span className="user-menu__copy">
            <strong>{user?.displayName ?? 'TeamPulse user'}</strong>
            <small>Sign out · {user?.email ?? 'Signed in'}</small>
          </span>
          <LogOut aria-hidden="true" size={16} />
        </button>
      </div>
    </header>
  )
}
