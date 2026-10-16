import { Bell, ChevronDown, Moon, Search, Sun } from 'lucide-react'

import { useTheme } from '@/app/providers/useTheme'

export function Topbar() {
  const { theme, toggleTheme } = useTheme()
  const nextTheme = theme === 'dark' ? 'light' : 'dark'

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

        <button className="user-menu" type="button" aria-label="Open user menu">
          <span className="user-menu__avatar" aria-hidden="true">
            BT
          </span>
          <span className="user-menu__copy">
            <strong>Bảo Trung</strong>
            <small>Leader</small>
          </span>
          <ChevronDown aria-hidden="true" size={16} />
        </button>
      </div>
    </header>
  )
}
