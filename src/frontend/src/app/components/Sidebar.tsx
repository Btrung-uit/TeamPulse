import { NavLink } from 'react-router-dom'

import { desktopNavigation } from '@/app/navigation/navigation'
import { useProject } from '@/features/projects/useProject'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

export function Sidebar() {
  const { currentWorkspace } = useWorkspace()
  const { currentProject } = useProject()
  const initials = currentWorkspace?.name
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase()

  return (
    <aside className="sidebar" aria-label="Primary navigation">
      <NavLink className="brand" to="/" aria-label="TeamPulse dashboard">
        <span className="brand__mark" aria-hidden="true">
          TP
        </span>
        <span className="brand__name">TeamPulse</span>
      </NavLink>

      <nav className="sidebar__navigation">
        {desktopNavigation(currentWorkspace!.id, currentProject?.id).map(
          (group) => (
            <div className="navigation-group" key={group.label}>
              <p className="navigation-group__label">{group.label}</p>
              <ul className="navigation-group__list">
                {group.items.map((item) => {
                  const Icon = item.icon

                  return (
                    <li key={item.to}>
                      <NavLink
                        className={({ isActive }) =>
                          `navigation-link${isActive ? ' is-active' : ''}`
                        }
                        end={item.end}
                        to={item.to}
                        title={item.label}
                      >
                        <Icon aria-hidden="true" size={19} strokeWidth={1.8} />
                        <span className="navigation-link__label">
                          {item.label}
                        </span>
                      </NavLink>
                    </li>
                  )
                })}
              </ul>
            </div>
          ),
        )}
      </nav>

      <div className="sidebar__workspace">
        <span className="workspace-avatar" aria-hidden="true">
          {initials || 'TP'}
        </span>
        <span className="sidebar__workspace-copy">
          <strong>{currentWorkspace?.name ?? 'Workspace'}</strong>
          <small>
            {currentWorkspace?.memberCount ?? 0}{' '}
            {(currentWorkspace?.memberCount ?? 0) === 1 ? 'member' : 'members'}
          </small>
        </span>
      </div>
    </aside>
  )
}
