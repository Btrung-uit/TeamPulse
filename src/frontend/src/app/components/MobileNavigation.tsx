import { NavLink } from 'react-router-dom'

import { mobileNavigation } from '@/app/navigation/navigation'
import { useProject } from '@/features/projects/useProject'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

export function MobileNavigation() {
  const { currentWorkspace } = useWorkspace()
  const { currentProject } = useProject()

  return (
    <nav className="mobile-navigation" aria-label="Mobile navigation">
      {mobileNavigation(currentWorkspace!.id, currentProject?.id).map(
        (item) => {
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
        },
      )}
    </nav>
  )
}
