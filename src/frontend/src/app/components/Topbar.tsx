import { Bell, ChevronDown, LogOut, Moon, Search, Sun } from 'lucide-react'

import { useTheme } from '@/app/providers/useTheme'
import { useAuth } from '@/features/auth/useAuth'

export function Topbar() {
  const { theme, toggleTheme } = useTheme()
  const { user, logout } = useAuth()
  const nextTheme = theme === 'dark' ? 'light' : 'dark'
  const initials = user?.displayName
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase()

  return (
    <header className="topbar">
      <button className="project-switcher" type="button">
        <span className="project-switcher__mark" aria-hidden="true">
          C
        </span>
        <span className="project-switcher__copy">
          <small>Current project</small>
          <strong>CampusEvent</strong>
        </span>
        <ChevronDown aria-hidden="true" size={16} />
      </button>

      <div className="topbar__actions">
        <button className="search-trigger" type="button" aria-label="Search">
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
          aria-label="Sign out"
          title="Sign out"
          onClick={() => void logout()}
        >
          <span className="user-menu__avatar" aria-hidden="true">
            {initials || 'TP'}
          </span>
          <span className="user-menu__copy">
            <strong>{user?.displayName ?? 'TeamPulse user'}</strong>
            <small>{user?.email ?? 'Signed in'}</small>
          </span>
          <LogOut aria-hidden="true" size={16} />
        </button>
      </div>
    </header>
  )
}
