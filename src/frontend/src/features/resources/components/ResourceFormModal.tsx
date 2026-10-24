import { useEffect, useState, type FormEvent } from 'react'

import { Button } from '@/components/ui/Button/Button'
import { Input } from '@/components/ui/Input/Input'
import { Modal } from '@/components/ui/Modal/Modal'
import { useToast } from '@/components/ui/Toast/useToast'
import { ResourceApiError } from '@/features/resources/resourceApi'
import {
  resourceTypeLabels,
  resourceTypes,
} from '@/features/resources/resourceModel'
import {
  useCreateResource,
  useUpdateResource,
} from '@/features/resources/resourceQueries'
import type {
  ProjectResource,
  ResourceType,
  SaveResourceInput,
} from '@/features/resources/types'
import { TaskSelectField } from '@/features/tasks/components/TaskSelectField'
import type { TaskSummary, WorkspaceMemberOption } from '@/features/tasks/types'

interface ResourceFormState {
  title: string
  type: ResourceType
  url: string
  externalId: string
  ownerUserId: string
  linkedTaskId: string
}

interface ResourceFormModalProps {
  isOpen: boolean
  workspaceId: string
  projectId: string
  resource?: ProjectResource
  members: WorkspaceMemberOption[]
  tasks: TaskSummary[]
  onClose: () => void
  onSaved: (resource: ProjectResource) => void
}

const githubRepositoryNamePattern = /^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/

function formFor(resource?: ProjectResource): ResourceFormState {
  return {
    title: resource?.title ?? '',
    type: resource?.type ?? 'GoogleDocs',
    url: resource?.url ?? '',
    externalId: resource?.externalId ?? '',
    ownerUserId: resource?.owner.userId ?? '',
    linkedTaskId: resource?.linkedTask?.id ?? '',
  }
}

export function ResourceFormModal({
  isOpen,
  workspaceId,
  projectId,
  resource,
  members,
  tasks,
  onClose,
  onSaved,
}: ResourceFormModalProps) {
  const { showToast } = useToast()
  const createResource = useCreateResource(workspaceId, projectId)
  const updateResource = useUpdateResource(
    workspaceId,
    projectId,
    resource?.id ?? '',
  )
  const [form, setForm] = useState(() => formFor(resource))
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string>()
  const mutation = resource ? updateResource : createResource

  useEffect(() => {
    if (!isOpen) return
    setForm(formFor(resource))
    setFieldErrors({})
    setFormError(undefined)
  }, [isOpen, resource])

  function update<K extends keyof ResourceFormState>(
    field: K,
    value: ResourceFormState[K],
  ) {
    setForm((current) => ({ ...current, [field]: value }))
    setFieldErrors((current) => {
      const next = { ...current }
      delete next[field]
      return next
    })
  }

  function validate() {
    const errors: Record<string, string> = {}
    const title = form.title.trim()
    const url = form.url.trim()
    const externalId = form.externalId.trim()
    if (!title || title.length > 200) {
      errors.title = 'Enter a title with no more than 200 characters.'
    }
    try {
      const parsed = new URL(url)
      if (!['http:', 'https:'].includes(parsed.protocol)) throw new Error()
    } catch {
      errors.url = 'Enter an absolute HTTP or HTTPS URL.'
    }
    if (externalId.length > 256) {
      errors.externalId = 'External ID cannot exceed 256 characters.'
    } else if (
      form.type === 'GitHub' &&
      !githubRepositoryNamePattern.test(externalId)
    ) {
      errors.externalId =
        'Enter the GitHub repository as owner/repository (for example, Btrung-uit/TeamPulse).'
    }
    if (!form.ownerUserId) errors.ownerUserId = 'Choose an owner.'
    setFieldErrors(errors)
    if (Object.keys(errors).length) return null

    return {
      title,
      type: form.type,
      url,
      externalId: externalId || undefined,
      ownerUserId: form.ownerUserId,
      linkedTaskId: form.linkedTaskId || undefined,
    } satisfies SaveResourceInput
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const input = validate()
    if (!input) return
    setFormError(undefined)

    try {
      const saved = await mutation.mutateAsync(input)
      onSaved(saved)
      onClose()
      showToast({
        title: resource ? 'Resource updated' : 'Resource added',
        description: `${saved.title} is available in the project hub.`,
        variant: 'success',
      })
    } catch (caught) {
      if (caught instanceof ResourceApiError) {
        setFieldErrors(
          Object.fromEntries(
            Object.entries(caught.body.errors ?? {}).map(
              ([field, messages]) => [field, messages[0]],
            ),
          ),
        )
        setFormError(caught.body.message)
      } else {
        setFormError('The resource could not be saved. Please try again.')
      }
    }
  }

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={resource ? 'Edit resource metadata' : 'Add external resource'}
      description="TeamPulse stores the link and coordination metadata. Editing stays in the original service."
      size="lg"
      isPending={mutation.isPending}
      footer={
        <>
          <Button
            variant="secondary"
            disabled={mutation.isPending}
            onClick={onClose}
          >
            Cancel
          </Button>
          <Button
            type="submit"
            form="resource-form"
            isLoading={mutation.isPending}
            loadingLabel="Saving resource"
          >
            {resource ? 'Save changes' : 'Add resource'}
          </Button>
        </>
      }
    >
      <form
        id="resource-form"
        className="resource-form"
        onSubmit={(event) => void submit(event)}
      >
        <div className="resource-form__row">
          <Input
            label="Title"
            value={form.title}
            error={fieldErrors.title}
            maxLength={200}
            onChange={(event) => update('title', event.target.value)}
          />
          <TaskSelectField
            label="Type"
            name="resource-type"
            value={form.type}
            error={fieldErrors.type}
            onChange={(event) =>
              update('type', event.target.value as ResourceType)
            }
          >
            {resourceTypes.map((type) => (
              <option key={type} value={type}>
                {resourceTypeLabels[type]}
              </option>
            ))}
          </TaskSelectField>
        </div>
        <Input
          label="External URL"
          type="url"
          placeholder="https://..."
          value={form.url}
          error={fieldErrors.url}
          onChange={(event) => update('url', event.target.value)}
        />
        <Input
          label={
            form.type === 'GitHub'
              ? 'Repository (owner/repository)'
              : 'External ID (optional)'
          }
          helperText={
            form.type === 'GitHub'
              ? 'Required to route repository webhook events to this project.'
              : undefined
          }
          placeholder={form.type === 'GitHub' ? 'owner/repository' : undefined}
          value={form.externalId}
          error={fieldErrors.externalId}
          maxLength={256}
          onChange={(event) => update('externalId', event.target.value)}
        />
        <div className="resource-form__row">
          <TaskSelectField
            label="Owner"
            name="resource-owner"
            value={form.ownerUserId}
            error={fieldErrors.ownerUserId}
            onChange={(event) => update('ownerUserId', event.target.value)}
          >
            <option value="">Choose owner</option>
            {members.map((member) => (
              <option key={member.userId} value={member.userId}>
                {member.displayName}
              </option>
            ))}
          </TaskSelectField>
          <TaskSelectField
            label="Linked task (optional)"
            name="resource-task"
            value={form.linkedTaskId}
            error={fieldErrors.linkedTaskId}
            onChange={(event) => update('linkedTaskId', event.target.value)}
          >
            <option value="">No linked task</option>
            {tasks.map((task) => (
              <option key={task.id} value={task.id}>
                {task.code} · {task.title}
              </option>
            ))}
          </TaskSelectField>
        </div>
        {formError && (
          <p className="resource-form__error" role="alert">
            {formError}
          </p>
        )}
      </form>
    </Modal>
  )
}
