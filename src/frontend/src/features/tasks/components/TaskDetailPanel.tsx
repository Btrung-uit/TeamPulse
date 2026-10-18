import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent as ReactKeyboardEvent,
} from 'react'
import { createPortal } from 'react-dom'
import {
  AlertTriangle,
  Archive,
  ArrowRight,
  ExternalLink,
  GitBranch,
  Link2,
  MessageSquareText,
  RefreshCw,
  Trash2,
  X,
} from 'lucide-react'

import { Button } from '@/components/ui/Button/Button'
import { Input, Textarea } from '@/components/ui/Input/Input'
import { Modal } from '@/components/ui/Modal/Modal'
import { StatusBadge } from '@/components/ui/StatusBadge/StatusBadge'
import { useToast } from '@/components/ui/Toast/useToast'
import { useAuth } from '@/features/auth/useAuth'
import { TaskSelectField } from '@/features/tasks/components/TaskSelectField'
import { GitCommandGenerator } from '@/features/tasks/components/GitCommandGenerator'
import { TaskApiError } from '@/features/tasks/taskApi'
import {
  allowedStatusTransitions,
  taskStatusLabels,
} from '@/features/tasks/taskDetailModel'
import { statusForCard } from '@/features/tasks/taskBoardModel'
import {
  useAddTaskEvidence,
  useArchiveTask,
  useChangeTaskStatus,
  useCreateTaskDependency,
  useDeleteTaskDependency,
  useTaskDependencies,
  useTaskDetails,
  useTaskCollaboration,
  useTaskOptions,
  useTasks,
  useUpdateTask,
  useReviewTask,
} from '@/features/tasks/taskQueries'
import type {
  TaskDetails,
  TaskEvidenceType,
  TaskPriority,
  TaskStatus,
  UpdateTaskInput,
} from '@/features/tasks/types'
import type { WorkspaceSummary } from '@/features/workspaces/types'

interface TaskDetailPanelProps {
  taskId?: string
  workspaceId: string
  projectId: string
  role: WorkspaceSummary['role']
  managementMode?: boolean
  onClose: () => void
}

interface TaskDraft {
  title: string
  description: string
  assigneeUserId: string
  reviewerUserId: string
  sprintId: string
  priority: TaskPriority
  dueOn: string
  estimateHours: string
}

type ConflictAction =
  | { kind: 'update' }
  | { kind: 'status'; status: TaskStatus }
  | { kind: 'archive' }

function toDraft(task: TaskDetails): TaskDraft {
  return {
    title: task.title,
    description: task.description ?? '',
    assigneeUserId: task.assignee?.userId ?? '',
    reviewerUserId: task.reviewer?.userId ?? '',
    sprintId: task.sprint?.id ?? '',
    priority: task.priority,
    dueOn: task.dueOn ?? '',
    estimateHours: task.estimateHours?.toString() ?? '',
  }
}

function taskError(error: unknown, fallback: string) {
  return error instanceof TaskApiError ? error.body.message : fallback
}

export function TaskDetailPanel({
  taskId,
  workspaceId,
  projectId,
  role,
  managementMode = true,
  onClose,
}: TaskDetailPanelProps) {
  const { user } = useAuth()
  const { showToast } = useToast()
  const closeButtonRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLElement>(null)
  const details = useTaskDetails(workspaceId, projectId, taskId)
  const collaboration = useTaskCollaboration(workspaceId, projectId, taskId)
  const options = useTaskOptions(workspaceId, projectId, Boolean(taskId))
  const allTasks = useTasks(workspaceId, projectId, {}, Boolean(taskId))
  const dependencies = useTaskDependencies(
    workspaceId,
    projectId,
    Boolean(taskId),
  )
  const updateTask = useUpdateTask(workspaceId, projectId, taskId ?? '')
  const changeStatus = useChangeTaskStatus(workspaceId, projectId, taskId ?? '')
  const archiveTask = useArchiveTask(workspaceId, projectId, taskId ?? '')
  const addEvidence = useAddTaskEvidence(workspaceId, projectId, taskId ?? '')
  const reviewTask = useReviewTask(workspaceId, projectId, taskId ?? '')
  const createDependency = useCreateTaskDependency(workspaceId, projectId)
  const deleteDependency = useDeleteTaskDependency(workspaceId, projectId)
  const [draft, setDraft] = useState<TaskDraft>()
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string>()
  const [conflict, setConflict] = useState<ConflictAction>()
  const [isArchiveOpen, setIsArchiveOpen] = useState(false)
  const [predecessorId, setPredecessorId] = useState('')
  const [evidenceType, setEvidenceType] = useState<TaskEvidenceType>('Commit')
  const [evidenceUrl, setEvidenceUrl] = useState('')
  const [evidenceNote, setEvidenceNote] = useState('')
  const [reviewComment, setReviewComment] = useState('')
  const [collaborationError, setCollaborationError] = useState<string>()

  const task = details.data
  const canManage = managementMode && role === 'Leader' && !task?.isArchived
  const canAddEvidence = Boolean(
    managementMode &&
    task &&
    !task.isArchived &&
    (role === 'Leader' || task.assignee?.userId === user?.id),
  )
  const canReview = Boolean(
    managementMode &&
    task &&
    !task.isArchived &&
    role === 'Leader' &&
    task.status === 'InReview',
  )
  const statusTargets = task
    ? allowedStatusTransitions(task, role, user?.id ?? '')
    : []
  const incoming = useMemo(
    () =>
      (dependencies.data ?? []).filter(
        (dependency) => dependency.successor.id === taskId,
      ),
    [dependencies.data, taskId],
  )
  const outgoing = useMemo(
    () =>
      (dependencies.data ?? []).filter(
        (dependency) => dependency.predecessor.id === taskId,
      ),
    [dependencies.data, taskId],
  )
  const predecessorOptions = (allTasks.data ?? []).filter(
    (candidate) =>
      candidate.id !== taskId &&
      !candidate.isArchived &&
      !incoming.some((edge) => edge.predecessor.id === candidate.id),
  )
  const isMutating =
    updateTask.isPending ||
    changeStatus.isPending ||
    archiveTask.isPending ||
    addEvidence.isPending ||
    reviewTask.isPending ||
    createDependency.isPending ||
    deleteDependency.isPending

  useEffect(() => {
    if (!task) return
    setDraft(toDraft(task))
    setFieldErrors({})
    setFormError(undefined)
    setConflict(undefined)
  }, [task])

  useEffect(() => {
    if (!taskId) return
    const previousFocus = document.activeElement as HTMLElement | null
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    const frame = window.requestAnimationFrame(() =>
      closeButtonRef.current?.focus(),
    )

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape' && !isArchiveOpen) onClose()
    }

    document.addEventListener('keydown', closeOnEscape)
    return () => {
      window.cancelAnimationFrame(frame)
      document.removeEventListener('keydown', closeOnEscape)
      document.body.style.overflow = previousOverflow
      previousFocus?.focus()
    }
  }, [isArchiveOpen, onClose, taskId])

  if (!taskId) return null

  function updateDraft<K extends keyof TaskDraft>(
    field: K,
    value: TaskDraft[K],
  ) {
    setDraft((current) => (current ? { ...current, [field]: value } : current))
    setFieldErrors((current) => {
      const next = { ...current }
      delete next[field]
      return next
    })
  }

  function trapPanelFocus(event: ReactKeyboardEvent<HTMLElement>) {
    if (event.key !== 'Tab' || !panelRef.current) return
    const focusable = Array.from(
      panelRef.current.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])',
      ),
    )
    if (focusable.length === 0) return
    const first = focusable[0]
    const last = focusable.at(-1)
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault()
      last?.focus()
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault()
      first.focus()
    }
  }

  function buildUpdateInput(rowVersion: string): UpdateTaskInput | null {
    if (!task || !draft) return null
    const errors: Record<string, string> = {}
    const title = draft.title.trim()
    const description = draft.description.trim()
    const estimate = draft.estimateHours
      ? Number(draft.estimateHours)
      : undefined

    if (!title || title.length > 240) {
      errors.title = 'Enter a title with no more than 240 characters.'
    }
    if (description.length > 5000) {
      errors.description = 'Description cannot exceed 5,000 characters.'
    }
    if (estimate !== undefined && (estimate <= 0 || estimate > 9999.99)) {
      errors.estimateHours = 'Estimate must be between 0.01 and 9,999.99 hours.'
    }
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) return null

    return {
      code: task.code,
      title,
      description: description || undefined,
      assigneeUserId: draft.assigneeUserId || undefined,
      reviewerUserId: draft.reviewerUserId || undefined,
      sprintId: draft.sprintId || undefined,
      priority: draft.priority,
      dueOn: draft.dueOn || undefined,
      estimateHours: estimate,
      rowVersion,
    }
  }

  function handleMutationError(error: unknown, action: ConflictAction) {
    if (error instanceof TaskApiError && error.body.code === 'task_conflict') {
      setConflict(action)
      setFormError(undefined)
      return
    }

    if (error instanceof TaskApiError && error.body.errors) {
      setFieldErrors(
        Object.fromEntries(
          Object.entries(error.body.errors).map(([field, messages]) => [
            field,
            messages[0],
          ]),
        ),
      )
    }
    setFormError(taskError(error, 'The task could not be changed. Try again.'))
  }

  async function saveTask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!task) return
    const input = buildUpdateInput(task.rowVersion)
    if (!input) return

    setFormError(undefined)
    setConflict(undefined)
    try {
      const updated = await updateTask.mutateAsync(input)
      setDraft(toDraft(updated))
      showToast({ title: 'Task updated', variant: 'success' })
    } catch (error) {
      handleMutationError(error, { kind: 'update' })
    }
  }

  async function changeTaskStatus(status: TaskStatus, rowVersion?: string) {
    if (!task) return
    setFormError(undefined)
    setConflict(undefined)
    try {
      const updated = await changeStatus.mutateAsync({
        status,
        rowVersion: rowVersion ?? task.rowVersion,
      })
      showToast({
        title: 'Status updated',
        description: `${updated.code} is now ${taskStatusLabels[updated.status]}.`,
        variant: 'success',
      })
    } catch (error) {
      handleMutationError(error, { kind: 'status', status })
    }
  }

  async function confirmArchive(rowVersion?: string) {
    if (!task) return
    setFormError(undefined)
    setConflict(undefined)
    try {
      await archiveTask.mutateAsync(rowVersion ?? task.rowVersion)
      setIsArchiveOpen(false)
      showToast({
        title: 'Task archived',
        description: `${task.code} remains available in project history.`,
        variant: 'success',
      })
      onClose()
    } catch (error) {
      setIsArchiveOpen(false)
      handleMutationError(error, { kind: 'archive' })
    }
  }

  async function reloadLatest() {
    const result = await details.refetch()
    if (result.data) setDraft(toDraft(result.data))
    setConflict(undefined)
    setFormError(undefined)
  }

  async function retryConflict() {
    if (!conflict) return
    const latest = await details.refetch()
    if (!latest.data) return

    if (conflict.kind === 'update') {
      const input = buildUpdateInput(latest.data.rowVersion)
      if (!input) return
      try {
        await updateTask.mutateAsync(input)
        setConflict(undefined)
        showToast({
          title: 'Task updated with the latest version',
          variant: 'success',
        })
      } catch (error) {
        handleMutationError(error, conflict)
      }
    } else if (conflict.kind === 'status') {
      await changeTaskStatus(conflict.status, latest.data.rowVersion)
    } else {
      await confirmArchive(latest.data.rowVersion)
    }
  }

  async function addDependency() {
    if (!predecessorId || !task) return
    setFormError(undefined)
    try {
      await createDependency.mutateAsync({
        predecessorTaskId: predecessorId,
        successorTaskId: task.id,
      })
      setPredecessorId('')
      showToast({ title: 'Dependency added', variant: 'success' })
    } catch (error) {
      setFormError(taskError(error, 'The dependency could not be added.'))
    }
  }

  async function removeDependency(
    predecessorTaskId: string,
    successorTaskId: string,
  ) {
    setFormError(undefined)
    try {
      await deleteDependency.mutateAsync({ predecessorTaskId, successorTaskId })
      showToast({ title: 'Dependency removed', variant: 'success' })
    } catch (error) {
      setFormError(taskError(error, 'The dependency could not be removed.'))
    }
  }

  async function submitEvidence(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const url = evidenceUrl.trim()
    let parsedUrl: URL
    try {
      parsedUrl = new URL(url)
    } catch {
      setCollaborationError('Enter an absolute HTTP or HTTPS URL.')
      return
    }
    if (!['http:', 'https:'].includes(parsedUrl.protocol)) {
      setCollaborationError('Enter an absolute HTTP or HTTPS URL.')
      return
    }

    setCollaborationError(undefined)
    try {
      await addEvidence.mutateAsync({
        type: evidenceType,
        url,
        note: evidenceNote.trim() || undefined,
      })
      setEvidenceUrl('')
      setEvidenceNote('')
      showToast({
        title: 'Evidence linked',
        description: 'The link is now part of this task history.',
        variant: 'success',
      })
    } catch (error) {
      setCollaborationError(
        taskError(error, 'The evidence could not be linked.'),
      )
    }
  }

  async function submitReview(decision: 'Accept' | 'RequestChanges') {
    if (!task) return
    const comment = reviewComment.trim()
    if (!comment) {
      setCollaborationError('Add a review comment before recording a decision.')
      return
    }

    setCollaborationError(undefined)
    try {
      await reviewTask.mutateAsync({
        decision,
        comment,
        rowVersion: task.rowVersion,
      })
      setReviewComment('')
      showToast({
        title: decision === 'Accept' ? 'Task accepted' : 'Changes requested',
        variant: 'success',
      })
    } catch (error) {
      setCollaborationError(
        taskError(error, 'The review could not be recorded.'),
      )
    }
  }

  const panel = (
    <div
      className="task-detail-backdrop"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !isMutating) onClose()
      }}
    >
      <aside
        ref={panelRef}
        className="task-detail-panel"
        role="dialog"
        aria-modal="true"
        aria-label={task ? `${task.code} task details` : 'Task details'}
        aria-busy={details.isLoading || isMutating || undefined}
        onKeyDown={trapPanelFocus}
      >
        <header className="task-detail-panel__header">
          <div>
            <p className="eyebrow">{task?.code ?? 'Task detail'}</p>
            <h2>{task?.title ?? 'Loading task'}</h2>
          </div>
          <Button
            ref={closeButtonRef}
            iconOnly
            variant="ghost"
            size="sm"
            aria-label="Close task detail"
            title="Close task detail"
            disabled={isMutating}
            onClick={onClose}
          >
            <X aria-hidden="true" size={19} />
          </Button>
        </header>

        {details.isLoading ? (
          <div className="task-detail-state" aria-label="Loading task details">
            <span className="task-detail-skeleton" />
            <span className="task-detail-skeleton" />
            <span className="task-detail-skeleton" />
          </div>
        ) : details.isError || !task || !draft ? (
          <div className="task-detail-state" role="alert">
            <AlertTriangle aria-hidden="true" size={28} />
            <h3>Task detail is unavailable</h3>
            <p>The board context is unchanged. Retry loading this task.</p>
            <Button
              leadingIcon={<RefreshCw size={16} />}
              onClick={() => void details.refetch()}
            >
              Try again
            </Button>
          </div>
        ) : (
          <>
            <div className="task-detail-panel__body">
              {task.isArchived && (
                <div className="task-detail-notice">
                  <Archive aria-hidden="true" size={17} />
                  This task is archived and read-only.
                </div>
              )}

              {conflict && (
                <div className="task-conflict" role="alert">
                  <AlertTriangle aria-hidden="true" size={19} />
                  <div>
                    <strong>This task changed after you opened it.</strong>
                    <p>
                      Review the latest data, or explicitly retry your action
                      against the newest version.
                    </p>
                    <div>
                      <Button
                        size="sm"
                        variant="secondary"
                        onClick={() => void reloadLatest()}
                      >
                        Review latest
                      </Button>
                      <Button size="sm" onClick={() => void retryConflict()}>
                        Retry my action
                      </Button>
                    </div>
                  </div>
                </div>
              )}

              <section
                className="task-detail-section"
                aria-labelledby="task-status-title"
              >
                <div className="task-detail-section__heading">
                  <div>
                    <p className="eyebrow">Workflow</p>
                    <h3 id="task-status-title">Status</h3>
                  </div>
                  <StatusBadge
                    status={
                      task.isArchived ? 'archived' : statusForCard[task.status]
                    }
                  />
                </div>
                {statusTargets.length > 0 ? (
                  <div className="task-status-actions">
                    {statusTargets.map((status) => (
                      <Button
                        key={status}
                        size="sm"
                        variant={status === 'Blocked' ? 'secondary' : 'primary'}
                        trailingIcon={<ArrowRight size={14} />}
                        disabled={isMutating}
                        onClick={() => void changeTaskStatus(status)}
                      >
                        {taskStatusLabels[status]}
                      </Button>
                    ))}
                  </div>
                ) : (
                  <p className="task-detail-help">
                    {task.isArchived || task.status === 'Done'
                      ? 'No further status transitions are available.'
                      : 'Your role or task assignment does not allow a status change.'}
                  </p>
                )}
              </section>

              <form
                id="task-detail-form"
                className="task-detail-form"
                onSubmit={(event) => void saveTask(event)}
              >
                <section
                  className="task-detail-section"
                  aria-labelledby="task-content-title"
                >
                  <div className="task-detail-section__heading">
                    <div>
                      <p className="eyebrow">Definition</p>
                      <h3 id="task-content-title">Task content</h3>
                    </div>
                    {!canManage && (
                      <span className="task-readonly-label">Read only</span>
                    )}
                  </div>
                  <Input
                    label="Title"
                    maxLength={240}
                    value={draft.title}
                    readOnly={!canManage}
                    error={fieldErrors.title}
                    onChange={(event) =>
                      updateDraft('title', event.target.value)
                    }
                  />
                  <Textarea
                    label="Description"
                    rows={5}
                    maxLength={5000}
                    value={draft.description}
                    readOnly={!canManage}
                    error={fieldErrors.description}
                    helperText={
                      canManage
                        ? `${draft.description.length}/5000 characters`
                        : undefined
                    }
                    onChange={(event) =>
                      updateDraft('description', event.target.value)
                    }
                  />
                </section>

                <section
                  className="task-detail-section"
                  aria-labelledby="task-planning-title"
                >
                  <div className="task-detail-section__heading">
                    <div>
                      <p className="eyebrow">Planning</p>
                      <h3 id="task-planning-title">Ownership and estimate</h3>
                    </div>
                  </div>
                  <div className="task-detail-form__row">
                    <TaskSelectField
                      label="Assignee"
                      name="detail-assignee"
                      value={draft.assigneeUserId}
                      disabled={!canManage || options.members.isLoading}
                      onChange={(event) =>
                        updateDraft('assigneeUserId', event.target.value)
                      }
                    >
                      <option value="">Unassigned</option>
                      {(options.members.data ?? []).map((member) => (
                        <option key={member.userId} value={member.userId}>
                          {member.displayName} · {member.role}
                        </option>
                      ))}
                    </TaskSelectField>
                    <TaskSelectField
                      label="Reviewer"
                      name="detail-reviewer"
                      value={draft.reviewerUserId}
                      disabled={!canManage || options.members.isLoading}
                      onChange={(event) =>
                        updateDraft('reviewerUserId', event.target.value)
                      }
                    >
                      <option value="">No reviewer</option>
                      {(options.members.data ?? []).map((member) => (
                        <option key={member.userId} value={member.userId}>
                          {member.displayName} · {member.role}
                        </option>
                      ))}
                    </TaskSelectField>
                  </div>
                  <div className="task-detail-form__row">
                    <TaskSelectField
                      label="Sprint"
                      name="detail-sprint"
                      value={draft.sprintId}
                      disabled={!canManage || options.sprints.isLoading}
                      onChange={(event) =>
                        updateDraft('sprintId', event.target.value)
                      }
                    >
                      <option value="">Backlog / no sprint</option>
                      {(options.sprints.data ?? []).map((sprint) => (
                        <option key={sprint.id} value={sprint.id}>
                          {sprint.name}
                        </option>
                      ))}
                    </TaskSelectField>
                    <TaskSelectField
                      label="Priority"
                      name="detail-priority"
                      value={draft.priority}
                      disabled={!canManage}
                      onChange={(event) =>
                        updateDraft(
                          'priority',
                          event.target.value as TaskPriority,
                        )
                      }
                    >
                      <option value="Low">Low</option>
                      <option value="Medium">Medium</option>
                      <option value="High">High</option>
                      <option value="Urgent">Urgent</option>
                    </TaskSelectField>
                  </div>
                  <div className="task-detail-form__row">
                    <Input
                      label="Deadline"
                      type="date"
                      value={draft.dueOn}
                      readOnly={!canManage}
                      error={fieldErrors.dueOn}
                      onChange={(event) =>
                        updateDraft('dueOn', event.target.value)
                      }
                    />
                    <Input
                      label="Estimate hours"
                      type="number"
                      min="0.01"
                      max="9999.99"
                      step="any"
                      value={draft.estimateHours}
                      readOnly={!canManage}
                      error={fieldErrors.estimateHours}
                      onChange={(event) =>
                        updateDraft('estimateHours', event.target.value)
                      }
                    />
                  </div>
                </section>
              </form>

              {managementMode && !task.isArchived && (
                <GitCommandGenerator
                  taskCode={task.code}
                  taskTitle={task.title}
                />
              )}

              <section
                className="task-detail-section"
                aria-labelledby="task-evidence-title"
              >
                <div className="task-detail-section__heading">
                  <div>
                    <p className="eyebrow">Proof of work</p>
                    <h3 id="task-evidence-title">Evidence and review</h3>
                  </div>
                  <Link2 aria-hidden="true" size={18} />
                </div>

                {canAddEvidence && (
                  <form
                    className="task-evidence-form"
                    onSubmit={(event) => void submitEvidence(event)}
                  >
                    <TaskSelectField
                      label="Evidence type"
                      name="evidence-type"
                      value={evidenceType}
                      disabled={addEvidence.isPending}
                      onChange={(event) =>
                        setEvidenceType(event.target.value as TaskEvidenceType)
                      }
                    >
                      <option value="Commit">Commit</option>
                      <option value="PullRequest">Pull request</option>
                      <option value="Issue">Issue</option>
                      <option value="Document">Document</option>
                      <option value="Design">Design</option>
                      <option value="Demo">Demo</option>
                    </TaskSelectField>
                    <Input
                      label="Evidence URL"
                      type="url"
                      placeholder="https://github.com/org/repo/pull/42"
                      value={evidenceUrl}
                      disabled={addEvidence.isPending}
                      onChange={(event) => setEvidenceUrl(event.target.value)}
                    />
                    <Textarea
                      label="Evidence note"
                      rows={2}
                      maxLength={500}
                      value={evidenceNote}
                      disabled={addEvidence.isPending}
                      onChange={(event) => setEvidenceNote(event.target.value)}
                    />
                    <Button
                      type="submit"
                      size="sm"
                      isLoading={addEvidence.isPending}
                      loadingLabel="Linking evidence"
                    >
                      Link evidence
                    </Button>
                  </form>
                )}

                {collaboration.isLoading ? (
                  <p className="task-detail-help">Loading evidence…</p>
                ) : collaboration.isError ? (
                  <div className="task-collaboration-error" role="alert">
                    <span>Evidence history is unavailable.</span>
                    <Button
                      size="sm"
                      variant="secondary"
                      onClick={() => void collaboration.refetch()}
                    >
                      Retry
                    </Button>
                  </div>
                ) : (collaboration.data?.evidence.length ?? 0) === 0 ? (
                  <p className="task-detail-help">
                    No evidence has been linked yet.
                  </p>
                ) : (
                  <div className="task-evidence-list">
                    {collaboration.data?.evidence.map((item) => (
                      <article key={item.id} className="task-evidence-item">
                        <div>
                          <strong>
                            {item.type.replace('PullRequest', 'Pull request')}
                          </strong>
                          <span>
                            {item.submittedBy.displayName} ·{' '}
                            {new Date(item.createdAtUtc).toLocaleString()}
                          </span>
                        </div>
                        <a href={item.url} target="_blank" rel="noreferrer">
                          Open evidence
                          <ExternalLink aria-hidden="true" size={14} />
                        </a>
                        {item.note && <p>{item.note}</p>}
                      </article>
                    ))}
                  </div>
                )}

                {canReview && (
                  <div className="task-review-form">
                    <Textarea
                      label="Review comment"
                      rows={3}
                      maxLength={2000}
                      value={reviewComment}
                      disabled={reviewTask.isPending}
                      onChange={(event) => setReviewComment(event.target.value)}
                    />
                    <div>
                      <Button
                        size="sm"
                        variant="secondary"
                        disabled={reviewTask.isPending}
                        onClick={() => void submitReview('RequestChanges')}
                      >
                        Request changes
                      </Button>
                      <Button
                        size="sm"
                        isLoading={reviewTask.isPending}
                        loadingLabel="Recording review"
                        onClick={() => void submitReview('Accept')}
                      >
                        Accept task
                      </Button>
                    </div>
                  </div>
                )}

                {(collaboration.data?.comments.length ?? 0) > 0 && (
                  <div className="task-review-history">
                    <strong>
                      <MessageSquareText aria-hidden="true" size={15} />
                      Review history
                    </strong>
                    {collaboration.data?.comments.map((comment) => (
                      <article key={comment.id}>
                        <span>
                          {comment.kind === 'ReviewAccepted'
                            ? 'Accepted'
                            : 'Changes requested'}{' '}
                          by {comment.author.displayName}
                        </span>
                        <p>{comment.body}</p>
                      </article>
                    ))}
                  </div>
                )}

                {collaborationError && (
                  <p className="task-detail-error" role="alert">
                    {collaborationError}
                  </p>
                )}
              </section>

              <section
                className="task-detail-section"
                aria-labelledby="task-dependencies-title"
              >
                <div className="task-detail-section__heading">
                  <div>
                    <p className="eyebrow">Sequence</p>
                    <h3 id="task-dependencies-title">Dependencies</h3>
                  </div>
                  <GitBranch aria-hidden="true" size={18} />
                </div>
                {canManage && (
                  <div className="task-dependency-add">
                    <TaskSelectField
                      label="This task depends on"
                      name="detail-predecessor"
                      value={predecessorId}
                      disabled={
                        allTasks.isLoading || createDependency.isPending
                      }
                      onChange={(event) => setPredecessorId(event.target.value)}
                    >
                      <option value="">Select predecessor</option>
                      {predecessorOptions.map((candidate) => (
                        <option key={candidate.id} value={candidate.id}>
                          {candidate.code} · {candidate.title}
                        </option>
                      ))}
                    </TaskSelectField>
                    <Button
                      size="sm"
                      disabled={!predecessorId || createDependency.isPending}
                      onClick={() => void addDependency()}
                    >
                      Add
                    </Button>
                  </div>
                )}
                {dependencies.isLoading ? (
                  <p className="task-detail-help">Loading dependency graph…</p>
                ) : incoming.length === 0 && outgoing.length === 0 ? (
                  <p className="task-detail-help">
                    No dependencies are linked to this task.
                  </p>
                ) : (
                  <div className="task-dependency-groups">
                    {incoming.length > 0 && (
                      <div>
                        <strong>Depends on</strong>
                        {incoming.map((edge) => (
                          <div
                            className="task-dependency-row"
                            key={`in-${edge.predecessor.id}`}
                          >
                            <div>
                              <span>
                                {edge.predecessor.code} ·{' '}
                                {edge.predecessor.title}
                              </span>
                              {edge.risks.map((risk) => (
                                <small key={risk.code}>{risk.message}</small>
                              ))}
                            </div>
                            {canManage && (
                              <Button
                                iconOnly
                                size="sm"
                                variant="ghost"
                                aria-label={`Remove dependency on ${edge.predecessor.code}`}
                                title="Remove dependency"
                                onClick={() =>
                                  void removeDependency(
                                    edge.predecessor.id,
                                    task.id,
                                  )
                                }
                              >
                                <Trash2 size={16} />
                              </Button>
                            )}
                          </div>
                        ))}
                      </div>
                    )}
                    {outgoing.length > 0 && (
                      <div>
                        <strong>Blocks</strong>
                        {outgoing.map((edge) => (
                          <div
                            className="task-dependency-row"
                            key={`out-${edge.successor.id}`}
                          >
                            <div>
                              <span>
                                {edge.successor.code} · {edge.successor.title}
                              </span>
                            </div>
                            {canManage && (
                              <Button
                                iconOnly
                                size="sm"
                                variant="ghost"
                                aria-label={`Remove dependency to ${edge.successor.code}`}
                                title="Remove dependency"
                                onClick={() =>
                                  void removeDependency(
                                    task.id,
                                    edge.successor.id,
                                  )
                                }
                              >
                                <Trash2 size={16} />
                              </Button>
                            )}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </section>

              {formError && (
                <p className="task-detail-error" role="alert">
                  {formError}
                </p>
              )}

              <section className="task-detail-meta" aria-label="Task metadata">
                <span>
                  Created {new Date(task.createdAtUtc).toLocaleString()}
                </span>
                <span>
                  Updated {new Date(task.updatedAtUtc).toLocaleString()}
                </span>
              </section>

              {canManage && (
                <section className="task-detail-danger">
                  <div>
                    <strong>Archive task</strong>
                    <p>
                      Remove it from the active board while retaining history.
                    </p>
                  </div>
                  <Button
                    variant="danger"
                    leadingIcon={<Archive size={16} />}
                    onClick={() => setIsArchiveOpen(true)}
                  >
                    Archive
                  </Button>
                </section>
              )}
            </div>

            {canManage && (
              <footer className="task-detail-panel__footer">
                <Button
                  variant="secondary"
                  disabled={isMutating}
                  onClick={() => setDraft(toDraft(task))}
                >
                  Discard changes
                </Button>
                <Button
                  type="submit"
                  form="task-detail-form"
                  isLoading={updateTask.isPending}
                  loadingLabel="Saving task"
                >
                  Save changes
                </Button>
              </footer>
            )}
          </>
        )}
      </aside>

      <Modal
        isOpen={isArchiveOpen}
        onClose={() => setIsArchiveOpen(false)}
        title="Archive this task?"
        description="It will leave the active board, but its detail and activity history remain available."
        size="sm"
        isPending={archiveTask.isPending}
        footer={
          <>
            <Button
              variant="secondary"
              disabled={archiveTask.isPending}
              onClick={() => setIsArchiveOpen(false)}
            >
              Cancel
            </Button>
            <Button
              variant="danger"
              isLoading={archiveTask.isPending}
              loadingLabel="Archiving task"
              onClick={() => void confirmArchive()}
            >
              Archive task
            </Button>
          </>
        }
      >
        <p className="task-archive-confirm">
          This action does not delete project history or contribution evidence.
        </p>
      </Modal>
    </div>
  )

  return createPortal(panel, document.body)
}
