import { useState, type FormEvent } from 'react'

import { Button } from '@/components/ui/Button/Button'
import { Input, Textarea } from '@/components/ui/Input/Input'
import { Modal } from '@/components/ui/Modal/Modal'
import { useToast } from '@/components/ui/Toast/useToast'
import { TaskSelectField } from '@/features/tasks/components/TaskSelectField'
import { TaskApiError } from '@/features/tasks/taskApi'
import { useCreateTask, useTaskOptions } from '@/features/tasks/taskQueries'
import type { CreateTaskInput, TaskPriority } from '@/features/tasks/types'

const taskCodePattern = /^[A-Za-z][A-Za-z0-9]{1,9}-[0-9]{1,6}$/

interface TaskFormState {
  code: string
  title: string
  description: string
  assigneeUserId: string
  reviewerUserId: string
  sprintId: string
  priority: TaskPriority
  dueOn: string
  estimateHours: string
}

const initialForm: TaskFormState = {
  code: '',
  title: '',
  description: '',
  assigneeUserId: '',
  reviewerUserId: '',
  sprintId: '',
  priority: 'Medium',
  dueOn: '',
  estimateHours: '',
}

interface CreateTaskModalProps {
  isOpen: boolean
  workspaceId: string
  projectId: string
  onClose: () => void
}

export function CreateTaskModal({
  isOpen,
  workspaceId,
  projectId,
  onClose,
}: CreateTaskModalProps) {
  const { showToast } = useToast()
  const options = useTaskOptions(workspaceId, projectId)
  const createTask = useCreateTask(workspaceId, projectId)
  const [form, setForm] = useState<TaskFormState>(initialForm)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string>()
  const memberOptions = options.members.data ?? []
  const sprintOptions = options.sprints.data ?? []
  const optionsLoading = options.members.isLoading || options.sprints.isLoading
  const optionsError = options.members.isError || options.sprints.isError

  function resetAndClose() {
    setForm(initialForm)
    setFieldErrors({})
    setFormError(undefined)
    onClose()
  }

  function requestClose() {
    if (!createTask.isPending) resetAndClose()
  }

  function updateForm<K extends keyof TaskFormState>(
    field: K,
    value: TaskFormState[K],
  ) {
    setForm((current) => ({ ...current, [field]: value }))
    setFieldErrors((current) => {
      const next = { ...current }
      delete next[field]
      return next
    })
  }

  function validateForm() {
    const errors: Record<string, string> = {}
    const code = form.code.trim()
    const title = form.title.trim()
    const description = form.description.trim()
    const estimate = form.estimateHours ? Number(form.estimateHours) : undefined

    if (!taskCodePattern.test(code)) {
      errors.code = 'Use PREFIX-NUMBER, for example TP-123.'
    }
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
      code: code.toUpperCase(),
      title,
      description: description || undefined,
      assigneeUserId: form.assigneeUserId || undefined,
      reviewerUserId: form.reviewerUserId || undefined,
      sprintId: form.sprintId || undefined,
      priority: form.priority,
      dueOn: form.dueOn || undefined,
      estimateHours: estimate,
      rowVersion: null,
    } satisfies CreateTaskInput
  }

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const input = validateForm()
    if (!input) return

    setFormError(undefined)
    try {
      const created = await createTask.mutateAsync(input)
      resetAndClose()
      showToast({
        title: 'Task created',
        description: `${created.code} was added to Todo.`,
        variant: 'success',
      })
    } catch (caught) {
      if (caught instanceof TaskApiError) {
        const apiErrors = Object.fromEntries(
          Object.entries(caught.body.errors ?? {}).map(([field, messages]) => [
            field,
            messages[0],
          ]),
        )
        setFieldErrors(apiErrors)
        setFormError(caught.body.message)
      } else {
        setFormError('The task was not created. Please try again.')
      }
    }
  }

  return (
    <Modal
      isOpen={isOpen}
      onClose={requestClose}
      title="Create task"
      description="New work starts in Todo. Status changes stay governed by the project lifecycle."
      size="lg"
      isPending={createTask.isPending}
      footer={
        <>
          <Button
            variant="secondary"
            disabled={createTask.isPending}
            onClick={requestClose}
          >
            Cancel
          </Button>
          <Button
            type="submit"
            form="create-task-form"
            isLoading={createTask.isPending}
            loadingLabel="Creating task"
            disabled={optionsLoading}
          >
            Create task
          </Button>
        </>
      }
    >
      <form
        className="task-create-form"
        id="create-task-form"
        onSubmit={(event) => void handleCreate(event)}
      >
        <div className="task-create-form__row task-create-form__row--identity">
          <Input
            label="Task code"
            placeholder="TP-123"
            maxLength={17}
            autoComplete="off"
            value={form.code}
            error={fieldErrors.code}
            onChange={(event) => updateForm('code', event.target.value)}
          />
          <TaskSelectField
            label="Priority"
            name="priority"
            value={form.priority}
            error={fieldErrors.priority}
            onChange={(event) =>
              updateForm('priority', event.target.value as TaskPriority)
            }
          >
            <option value="Low">Low</option>
            <option value="Medium">Medium</option>
            <option value="High">High</option>
            <option value="Urgent">Urgent</option>
          </TaskSelectField>
        </div>
        <Input
          label="Title"
          placeholder="Describe a clear deliverable"
          maxLength={240}
          value={form.title}
          error={fieldErrors.title}
          onChange={(event) => updateForm('title', event.target.value)}
        />
        <Textarea
          label="Description"
          placeholder="Add context, constraints or an expected outcome"
          rows={4}
          maxLength={5000}
          value={form.description}
          error={fieldErrors.description}
          helperText={`${form.description.length}/5000 characters`}
          onChange={(event) => updateForm('description', event.target.value)}
        />
        <div className="task-create-form__row">
          <TaskSelectField
            label="Assignee"
            name="assigneeUserId"
            value={form.assigneeUserId}
            error={fieldErrors.assigneeUserId}
            disabled={options.members.isLoading || options.members.isError}
            onChange={(event) =>
              updateForm('assigneeUserId', event.target.value)
            }
          >
            <option value="">Unassigned</option>
            {memberOptions.map((member) => (
              <option key={member.userId} value={member.userId}>
                {member.displayName} · {member.role}
              </option>
            ))}
          </TaskSelectField>
          <TaskSelectField
            label="Reviewer"
            name="reviewerUserId"
            value={form.reviewerUserId}
            error={fieldErrors.reviewerUserId}
            disabled={options.members.isLoading || options.members.isError}
            onChange={(event) =>
              updateForm('reviewerUserId', event.target.value)
            }
          >
            <option value="">No reviewer</option>
            {memberOptions.map((member) => (
              <option key={member.userId} value={member.userId}>
                {member.displayName} · {member.role}
              </option>
            ))}
          </TaskSelectField>
        </div>
        <div className="task-create-form__row">
          <TaskSelectField
            label="Sprint"
            name="sprintId"
            value={form.sprintId}
            error={fieldErrors.sprintId}
            disabled={options.sprints.isLoading || options.sprints.isError}
            onChange={(event) => updateForm('sprintId', event.target.value)}
          >
            <option value="">Backlog / no sprint</option>
            {sprintOptions.map((sprint) => (
              <option key={sprint.id} value={sprint.id}>
                {sprint.name}
              </option>
            ))}
          </TaskSelectField>
          <Input
            label="Deadline"
            type="date"
            value={form.dueOn}
            error={fieldErrors.dueOn}
            onChange={(event) => updateForm('dueOn', event.target.value)}
          />
        </div>
        <Input
          label="Estimate hours"
          type="number"
          min="0.01"
          max="9999.99"
          step="any"
          placeholder="e.g. 4"
          value={form.estimateHours}
          error={fieldErrors.estimateHours}
          onChange={(event) => updateForm('estimateHours', event.target.value)}
        />
        {optionsError && (
          <div className="task-options-error" role="alert">
            <div>
              <strong>Some assignment options are unavailable.</strong>
              <span>You can create an unassigned task or retry loading.</span>
            </div>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => {
                void options.members.refetch()
                void options.sprints.refetch()
              }}
            >
              Retry options
            </Button>
          </div>
        )}
        {formError && (
          <p className="task-create-form__error" role="alert">
            {formError}
          </p>
        )}
      </form>
    </Modal>
  )
}
