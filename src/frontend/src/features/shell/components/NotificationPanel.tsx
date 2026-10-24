import { useCallback, useEffect, useState } from 'react'
import {
  Bell,
  CheckCheck,
  ClipboardCheck,
  GitPullRequest,
  ListTodo,
} from 'lucide-react'
import { useNavigate } from 'react-router-dom'

import { Button } from '@/components/ui/Button/Button'
import { useAuth } from '@/features/auth/useAuth'
import { projectPath } from '@/features/projects/projectPaths'
import {
  notificationApi,
  type NotificationFeed,
  type NotificationFilter,
  type NotificationItem,
} from '@/features/shell/shellApi'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

interface NotificationPanelProps {
  isOpen: boolean
  onClose: () => void
  onUnreadCountChange: (count: number) => void
  mode?: 'panel' | 'page'
}

const emptyFeed: NotificationFeed = { items: [], unreadCount: 0 }

export function NotificationPanel({
  isOpen,
  onClose,
  onUnreadCountChange,
  mode = 'panel',
}: NotificationPanelProps) {
  const { accessToken } = useAuth()
  const { currentWorkspace } = useWorkspace()
  const navigate = useNavigate()
  const [filter, setFilter] = useState<NotificationFilter>('all')
  const [feed, setFeed] = useState(emptyFeed)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(
    async (requestedFilter: NotificationFilter) => {
      if (!accessToken || !currentWorkspace) return

      setIsLoading(true)
      setError(null)
      try {
        const nextFeed = await notificationApi.list(
          accessToken,
          currentWorkspace.id,
          requestedFilter,
        )
        setFeed(nextFeed)
        onUnreadCountChange(nextFeed.unreadCount)
      } catch {
        setError('Notifications are unavailable right now.')
      } finally {
        setIsLoading(false)
      }
    },
    [accessToken, currentWorkspace, onUnreadCountChange],
  )

  useEffect(() => {
    setFilter('all')
    setFeed(emptyFeed)
    if (currentWorkspace) void load('all')
  }, [currentWorkspace, load])

  useEffect(() => {
    if (isOpen) void load(filter)
  }, [filter, isOpen, load])

  async function openNotification(notification: NotificationItem) {
    if (!accessToken || !currentWorkspace) return

    if (!notification.isRead) {
      try {
        await notificationApi.markRead(
          accessToken,
          currentWorkspace.id,
          notification.id,
        )
        const unreadCount = Math.max(0, feed.unreadCount - 1)
        setFeed((current) => ({
          ...current,
          unreadCount,
          items:
            filter === 'unread'
              ? current.items.filter((item) => item.id !== notification.id)
              : current.items.map((item) =>
                  item.id === notification.id
                    ? { ...item, isRead: true }
                    : item,
                ),
        }))
        onUnreadCountChange(unreadCount)
      } catch {
        // Navigation still works if the read receipt cannot be saved.
      }
    }

    onClose()
    if (notification.externalUrl) {
      window.open(notification.externalUrl, '_blank', 'noopener,noreferrer')
      return
    }

    const destination = notification.taskId
      ? `${projectPath(currentWorkspace.id, notification.projectId)}/tasks?task=${notification.taskId}`
      : projectPath(currentWorkspace.id, notification.projectId)
    navigate(destination)
  }

  async function markAllRead() {
    if (!accessToken || !currentWorkspace || feed.unreadCount === 0) return

    try {
      await notificationApi.markAllRead(accessToken, currentWorkspace.id)
      setFeed((current) => ({
        unreadCount: 0,
        items:
          filter === 'unread'
            ? []
            : current.items.map((item) => ({ ...item, isRead: true })),
      }))
      onUnreadCountChange(0)
    } catch {
      setError('Unable to mark notifications as read. Please try again.')
    }
  }

  if (!isOpen) return null

  return (
    <section
      className={`notification-panel notification-panel--${mode}`}
      role={mode === 'panel' ? 'dialog' : 'region'}
      aria-label="Notifications"
    >
      <header className="notification-panel__header">
        <div>
          <p>Inbox</p>
          <h2>Notifications</h2>
        </div>
        <Button
          variant="ghost"
          size="sm"
          disabled={feed.unreadCount === 0}
          leadingIcon={<CheckCheck size={16} />}
          onClick={() => void markAllRead()}
        >
          Mark all read
        </Button>
      </header>

      <div className="notification-panel__tabs" role="tablist">
        {(['all', 'unread', 'reviews'] as const).map((value) => (
          <button
            type="button"
            role="tab"
            aria-selected={filter === value}
            className={filter === value ? 'is-active' : undefined}
            key={value}
            onClick={() => setFilter(value)}
          >
            {value === 'all'
              ? 'All'
              : value === 'unread'
                ? `Unread (${feed.unreadCount})`
                : 'Reviews'}
          </button>
        ))}
      </div>

      <div className="notification-panel__content" aria-live="polite">
        {isLoading && <p className="notification-panel__state">Loading…</p>}
        {!isLoading && error && (
          <div className="notification-panel__state">
            <p>{error}</p>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => void load(filter)}
            >
              Try again
            </Button>
          </div>
        )}
        {!isLoading && !error && feed.items.length === 0 && (
          <div className="notification-panel__state">
            <Bell aria-hidden="true" size={24} />
            <p>No notifications in this view.</p>
          </div>
        )}
        {!isLoading && !error && feed.items.length > 0 && (
          <div className="notification-panel__list">
            {feed.items.map((notification) => {
              const Icon = notification.kind.startsWith('PullRequest')
                ? GitPullRequest
                : notification.kind === 'ReviewRequest'
                  ? ClipboardCheck
                  : ListTodo
              return (
                <button
                  className={`notification-item${notification.isRead ? '' : ' is-unread'}`}
                  type="button"
                  key={notification.id}
                  onClick={() => void openNotification(notification)}
                >
                  <span className="notification-item__icon" aria-hidden="true">
                    <Icon size={18} />
                  </span>
                  <span className="notification-item__copy">
                    <strong>{notification.title}</strong>
                    <span>{notification.message}</span>
                    <small>{formatTimestamp(notification.createdAtUtc)}</small>
                  </span>
                  {!notification.isRead && (
                    <span
                      className="notification-item__unread"
                      aria-label="Unread"
                    />
                  )}
                </button>
              )
            })}
          </div>
        )}
      </div>
    </section>
  )
}

function formatTimestamp(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}
