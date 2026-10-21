import { useState } from 'react'

import { NotificationPanel } from '@/features/shell/components/NotificationPanel'

export function NotificationsPage() {
  const [, setUnreadCount] = useState(0)

  return (
    <main className="notification-page">
      <NotificationPanel
        isOpen
        mode="page"
        onClose={() => undefined}
        onUnreadCountChange={setUnreadCount}
      />
    </main>
  )
}
