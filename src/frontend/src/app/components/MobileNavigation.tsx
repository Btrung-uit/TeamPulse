import { NavLink } from 'react-router-dom'

import { mobileNavigation } from '@/app/navigation/navigation'

export function MobileNavigation() {
  return (
    <nav className="mobile-navigation" aria-label="Mobile navigation">
      {mobileNavigation.map((item) => {
        const Icon = item.icon

        return (
          <NavLink
            className={({ isActive }) =>
              `mobile-navigation__link${isActive ? ' is-active' : ''}`
            }
            end={item.end}
            key={item.to}
            to={item.to}
          >
            <Icon aria-hidden="true" size={20} strokeWidth={1.8} />
            <span>{item.label}</span>
          </NavLink>
        )
      })}
    </nav>
  )
}
