import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { ToastProvider } from '@/components/ui/Toast/ToastProvider'
import { GitCommandGenerator } from '@/features/tasks/components/GitCommandGenerator'

describe('GitCommandGenerator', () => {
  it('requires explicit paths and copies the selected safe command', async () => {
    const user = userEvent.setup()
    const writeText = vi.spyOn(navigator.clipboard, 'writeText')
    render(
      <ToastProvider>
        <GitCommandGenerator
          taskCode="TP-033"
          taskTitle="Git Command Generator"
        />
      </ToastProvider>,
    )

    await user.click(screen.getByRole('button', { name: 'Get Git commands' }))
    expect(screen.getByText(/does not generate/i)).toHaveTextContent(
      'git add .',
    )
    expect(screen.getByRole('button', { name: 'Copy all' })).toBeDisabled()

    await user.selectOptions(screen.getByLabelText('Branch type'), 'fix')
    await user.selectOptions(screen.getByLabelText('Commit type'), 'fix')
    await user.clear(screen.getByLabelText('Commit summary'))
    await user.type(
      screen.getByLabelText('Commit summary'),
      'repair command output',
    )
    await user.type(
      screen.getByLabelText('Files to stage'),
      'src/frontend/src/file.ts',
    )

    expect(
      screen.getByText('fix/tp-033-git-command-generator'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('fix: repair command output #TP-033'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Copy all' })).toBeEnabled()

    await user.click(
      screen.getByRole('button', { name: 'Copy Stage explicit paths only' }),
    )
    expect(writeText).toHaveBeenCalledWith(
      'git add -- "src/frontend/src/file.ts"',
    )
    expect(await screen.findByText('Command copied')).toBeInTheDocument()
  })

  it('shows an error and keeps copy-all disabled for unsafe paths', async () => {
    const user = userEvent.setup()
    render(
      <ToastProvider>
        <GitCommandGenerator taskCode="TP-033" taskTitle="Generator" />
      </ToastProvider>,
    )

    await user.click(screen.getByRole('button', { name: 'Get Git commands' }))
    await user.type(screen.getByLabelText('Files to stage'), '../secret.txt')

    expect(screen.getByText(/Unsafe or non-explicit path/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Copy all' })).toBeDisabled()
    expect(screen.queryByText(/git add --/)).not.toBeInTheDocument()
  })
})
