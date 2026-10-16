import { useState } from 'react'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { FolderSearch, Save } from 'lucide-react'
import { describe, expect, it, vi } from 'vitest'

import { Button } from '@/components/ui/Button/Button'
import { EmptyState } from '@/components/ui/EmptyState/EmptyState'
import { Input } from '@/components/ui/Input/Input'
import { Modal } from '@/components/ui/Modal/Modal'
import { StatusBadge } from '@/components/ui/StatusBadge/StatusBadge'
import { Table, type TableColumn } from '@/components/ui/Table/Table'
import { TaskCard } from '@/components/ui/TaskCard/TaskCard'
import { ToastProvider } from '@/components/ui/Toast/ToastProvider'
import { useToast } from '@/components/ui/Toast/useToast'

describe('component primitives', () => {
  it('prevents button actions while loading and exposes the loading state', async () => {
    const user = userEvent.setup()
    const onClick = vi.fn()

    render(
      <Button isLoading loadingLabel="Saving task" onClick={onClick}>
        Save
      </Button>,
    )

    const button = screen.getByRole('button', { name: 'Saving task' })
    expect(button).toBeDisabled()
    expect(button).toHaveAttribute('aria-busy', 'true')
    await user.click(button)
    expect(onClick).not.toHaveBeenCalled()
  })

  it('connects input labels and validation messages accessibly', () => {
    render(
      <Input
        label="Assignee email"
        defaultValue="invalid"
        error="Enter a valid email."
      />,
    )

    const input = screen.getByLabelText('Assignee email')
    const error = screen.getByText('Enter a valid email.')
    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(input).toHaveAttribute('aria-describedby', error.id)
  })

  it('communicates status with an icon and visible text', () => {
    render(<StatusBadge status="blocked" />)
    expect(screen.getByText('Blocked')).toBeVisible()
  })

  it('traps modal behavior, closes with Escape, and restores focus', async () => {
    const user = userEvent.setup()

    function ModalHost() {
      const [isOpen, setIsOpen] = useState(false)
      return (
        <>
          <Button onClick={() => setIsOpen(true)}>Open archive dialog</Button>
          <Modal
            isOpen={isOpen}
            onClose={() => setIsOpen(false)}
            title="Archive project"
          >
            <p>History remains available.</p>
          </Modal>
        </>
      )
    }

    render(<ModalHost />)
    const trigger = screen.getByRole('button', { name: 'Open archive dialog' })
    await user.click(trigger)
    expect(
      screen.getByRole('dialog', { name: 'Archive project' }),
    ).toBeVisible()
    const closeButton = screen.getByRole('button', { name: 'Close dialog' })
    await waitFor(() => expect(closeButton).toHaveFocus())
    await user.tab()
    expect(closeButton).toHaveFocus()
    await user.tab({ shift: true })
    expect(closeButton).toHaveFocus()
    await user.keyboard('{Escape}')
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    )
    expect(trigger).toHaveFocus()
  })

  it('keeps only the three newest toast notifications', async () => {
    const user = userEvent.setup()

    function ToastHost() {
      const { showToast } = useToast()
      return (
        <>
          {['One', 'Two', 'Three', 'Four'].map((title) => (
            <button
              key={title}
              type="button"
              onClick={() => showToast({ title, duration: null })}
            >
              Show {title}
            </button>
          ))}
        </>
      )
    }

    render(
      <ToastProvider>
        <ToastHost />
      </ToastProvider>,
    )

    for (const title of ['One', 'Two', 'Three', 'Four']) {
      await user.click(screen.getByRole('button', { name: `Show ${title}` }))
    }

    expect(screen.queryByText('One')).not.toBeInTheDocument()
    expect(screen.getByText('Two')).toBeVisible()
    expect(screen.getByText('Three')).toBeVisible()
    expect(screen.getByText('Four')).toBeVisible()
  })

  it('renders a focused empty state with at most two actions', () => {
    render(
      <EmptyState
        icon={<FolderSearch />}
        title="No tasks found"
        description="Clear the active filters."
        primaryAction={<Button>Clear filters</Button>}
        secondaryAction={<Button variant="ghost">View all</Button>}
      />,
    )

    expect(
      screen.getByRole('heading', { name: 'No tasks found' }),
    ).toBeVisible()
    expect(screen.getAllByRole('button')).toHaveLength(2)
  })

  it('separates the task menu action from the card action', async () => {
    const user = userEvent.setup()
    const onOpen = vi.fn()
    const onMenuClick = vi.fn()

    render(
      <TaskCard
        code="TP-021"
        title="Build project flow"
        status="in-progress"
        priority="high"
        assignee={{ name: 'Bảo Trung', initials: 'BT' }}
        dueDate="Oct 18"
        onClick={onOpen}
        onMenuClick={onMenuClick}
      />,
    )

    await user.click(
      screen.getByRole('button', { name: 'Open actions for TP-021' }),
    )
    expect(onMenuClick).toHaveBeenCalledOnce()
    expect(onOpen).not.toHaveBeenCalled()

    await user.click(
      screen.getByRole('button', { name: 'Open TP-021: Build project flow' }),
    )
    expect(onOpen).toHaveBeenCalledOnce()
  })

  it('keeps table row actions independent from row selection', async () => {
    const user = userEvent.setup()
    const onRowClick = vi.fn()
    const onSave = vi.fn()
    const rows = [{ id: '1', title: 'Task one' }]
    const columns: TableColumn<(typeof rows)[number]>[] = [
      { key: 'title', header: 'Title', cell: (row) => row.title },
      {
        key: 'action',
        header: 'Action',
        isAction: true,
        cell: () => (
          <Button
            iconOnly
            aria-label="Save task"
            title="Save task"
            onClick={onSave}
          >
            <Save aria-hidden="true" />
          </Button>
        ),
      },
    ]

    render(
      <Table
        caption="Tasks"
        columns={columns}
        rows={rows}
        getRowId={(row) => row.id}
        onRowClick={onRowClick}
      />,
    )

    await user.click(screen.getByRole('button', { name: 'Save task' }))
    expect(onSave).toHaveBeenCalledOnce()
    expect(onRowClick).not.toHaveBeenCalled()

    const row = within(screen.getByRole('table'))
      .getByText('Task one')
      .closest('tr')
    expect(row).not.toBeNull()
    await user.click(row!)
    expect(onRowClick).toHaveBeenCalledOnce()
  })

  it('provides a useful table empty state by default', () => {
    render(
      <Table
        caption="Empty tasks"
        columns={[
          {
            key: 'title',
            header: 'Title',
            cell: (row: { title: string }) => row.title,
          },
        ]}
        rows={[]}
        getRowId={(row) => row.title}
      />,
    )

    expect(screen.getByText('No data yet')).toBeVisible()
    expect(
      screen.getByText('Items will appear here when they are available.'),
    ).toBeVisible()
  })
})
