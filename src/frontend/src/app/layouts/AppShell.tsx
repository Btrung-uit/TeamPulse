import { Outlet } from 'react-router-dom'

import { MobileNavigation } from '@/app/components/MobileNavigation'
import { Sidebar } from '@/app/components/Sidebar'
import { Topbar } from '@/app/components/Topbar'

export function AppShell() {
  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>
      <Sidebar />
      <div className="app-shell__body">
        <Topbar />
        <main className="app-shell__content" id="main-content" tabIndex={-1}>
          <Outlet />
        </main>
      </div>
      <MobileNavigation />
    </div>
  )
}
