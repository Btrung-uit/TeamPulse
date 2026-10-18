import { useMemo, useState } from 'react'
import {
  Check,
  Clipboard,
  GitBranch,
  ShieldCheck,
  Terminal,
} from 'lucide-react'

import { Button } from '@/components/ui/Button/Button'
import { Input, Textarea } from '@/components/ui/Input/Input'
import { Modal } from '@/components/ui/Modal/Modal'
import { useToast } from '@/components/ui/Toast/useToast'
import { TaskSelectField } from '@/features/tasks/components/TaskSelectField'
import {
  buildGitCommandPlan,
  defaultCommitSummary,
  gitBranchTypes,
  gitCommitTypes,
  type GitBranchType,
  type GitCommitType,
} from '@/features/tasks/gitCommandModel'

interface GitCommandGeneratorProps {
  taskCode: string
  taskTitle: string
}

export function GitCommandGenerator({
  taskCode,
  taskTitle,
}: GitCommandGeneratorProps) {
  const { showToast } = useToast()
  const [isOpen, setIsOpen] = useState(false)
  const [branchType, setBranchType] = useState<GitBranchType>('feature')
  const [commitType, setCommitType] = useState<GitCommitType>('feat')
  const [commitSummary, setCommitSummary] = useState(() =>
    defaultCommitSummary(taskTitle),
  )
  const [stagePaths, setStagePaths] = useState('')
  const plan = useMemo(
    () =>
      buildGitCommandPlan({
        taskCode,
        taskTitle,
        branchType,
        commitType,
        commitSummary,
        stagePaths,
      }),
    [branchType, commitSummary, commitType, stagePaths, taskCode, taskTitle],
  )
  const canCopyAll =
    !plan.needsStagePaths && plan.invalidStagePaths.length === 0

  async function copy(value: string, label: string) {
    try {
      await navigator.clipboard.writeText(value)
      showToast({
        title: 'Command copied',
        description: `${label} is ready to paste. Review it before running.`,
        variant: 'success',
      })
    } catch {
      showToast({
        title: 'Command was not copied',
        description: 'Your browser blocked clipboard access.',
        variant: 'error',
      })
    }
  }

  return (
    <section
      className="task-detail-section git-command-entry"
      aria-labelledby="git-command-title"
    >
      <div className="task-detail-section__heading">
        <div>
          <p className="eyebrow">Developer helper</p>
          <h3 id="git-command-title">Git command generator</h3>
        </div>
        <Terminal aria-hidden="true" size={18} />
      </div>
      <p className="task-detail-help">
        Generate a task branch and Conventional Commit guidance without
        executing anything on your machine.
      </p>
      <Button
        size="sm"
        variant="secondary"
        leadingIcon={<GitBranch size={16} />}
        onClick={() => setIsOpen(true)}
      >
        Get Git commands
      </Button>

      <Modal
        isOpen={isOpen}
        onClose={() => setIsOpen(false)}
        title={`Git commands for ${taskCode}`}
        description="Run commands one at a time from the repository root. Inspect every result before continuing."
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setIsOpen(false)}>
              Close
            </Button>
            <Button
              leadingIcon={<Clipboard size={16} />}
              disabled={!canCopyAll}
              onClick={() =>
                void copy(
                  plan.commands.map((item) => item.command).join('\n'),
                  'Command plan',
                )
              }
            >
              Copy all
            </Button>
          </>
        }
      >
        <div className="git-command-generator">
          <div className="git-command-form-row">
            <TaskSelectField
              label="Branch type"
              name="git-branch-type"
              value={branchType}
              onChange={(event) =>
                setBranchType(event.target.value as GitBranchType)
              }
            >
              {gitBranchTypes.map((type) => (
                <option key={type} value={type}>
                  {type}
                </option>
              ))}
            </TaskSelectField>
            <TaskSelectField
              label="Commit type"
              name="git-commit-type"
              value={commitType}
              onChange={(event) =>
                setCommitType(event.target.value as GitCommitType)
              }
            >
              {gitCommitTypes.map((type) => (
                <option key={type} value={type}>
                  {type}
                </option>
              ))}
            </TaskSelectField>
          </div>
          <Input
            label="Commit summary"
            maxLength={72}
            value={commitSummary}
            helperText={`Task Code #${taskCode} is appended automatically.`}
            onChange={(event) => setCommitSummary(event.target.value)}
          />
          <Textarea
            label="Files to stage"
            rows={3}
            placeholder={'src/path/file.ts\ntests/path/file.test.ts'}
            value={stagePaths}
            helperText="One repository-relative path per line. Wildcards, absolute paths and parent traversal are rejected."
            error={
              plan.invalidStagePaths.length
                ? `Unsafe or non-explicit path: ${plan.invalidStagePaths.join(', ')}`
                : undefined
            }
            onChange={(event) => setStagePaths(event.target.value)}
          />

          <div className="git-command-summary">
            <div>
              <span>Branch</span>
              <code>{plan.branchName}</code>
            </div>
            <div>
              <span>Commit</span>
              <code>{plan.commitMessage}</code>
            </div>
          </div>

          {plan.needsStagePaths && (
            <div className="git-command-warning" role="status">
              <ShieldCheck aria-hidden="true" size={18} />
              <div>
                <strong>
                  Add explicit paths before copying the full plan.
                </strong>
                <span>
                  TeamPulse deliberately does not generate{' '}
                  <code>git add .</code>.
                </span>
              </div>
            </div>
          )}

          <ol className="git-command-list">
            {plan.commands.map((item) => (
              <li key={item.id}>
                <div>
                  <span>{item.label}</span>
                  <code>{item.command}</code>
                </div>
                <Button
                  iconOnly
                  size="sm"
                  variant="ghost"
                  aria-label={`Copy ${item.label}`}
                  title="Copy command"
                  onClick={() => void copy(item.command, item.label)}
                >
                  <Clipboard size={15} />
                </Button>
              </li>
            ))}
          </ol>

          <div className="git-command-safety">
            <Check aria-hidden="true" size={16} />
            Push always targets the generated task branch, never main.
          </div>
        </div>
      </Modal>
    </section>
  )
}
