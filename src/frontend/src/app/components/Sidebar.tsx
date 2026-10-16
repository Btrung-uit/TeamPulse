import { NavLink } from 'react-router-dom'

import { desktopNavigation } from '@/app/navigation/navigation'

export function Sidebar() {
  return (
    <aside className="sidebar" aria-label="Primary navigation">
      <NavLink className="brand" to="/" aria-label="TeamPulse dashboard">
        <span className="brand__mark" aria-hidden="true">
          TP
        </span>
        <span className="brand__name">TeamPulse</span>
      </NavLink>

      <nav className="sidebar__navigation">
        {desktopNavigation.map((group) => (
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
        ))}
      </nav>

      <div className="sidebar__workspace">
        <span className="workspace-avatar" aria-hidden="true">
          CE
        </span>
        <span className="sidebar__workspace-copy">
          <strong>UIT Workspace</strong>
          <small>4 members</small>
        </span>
      </div>
    </aside>
  )
}
