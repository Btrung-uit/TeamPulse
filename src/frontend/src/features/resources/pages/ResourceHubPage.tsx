import { useMemo, useState } from 'react'
import {
  Box,
  Copy,
  ExternalLink,
  FileText,
  FolderOpen,
  GitBranch,
  HardDrive,
  MonitorPlay,
  Palette,
  Pencil,
  Plus,
  RefreshCw,
  Search,
  Trash2,
} from 'lucide-react'
import { useSearchParams } from 'react-router-dom'

import { Button } from '@/components/ui/Button/Button'
import { EmptyState } from '@/components/ui/EmptyState/EmptyState'
import { Input } from '@/components/ui/Input/Input'
import { Modal } from '@/components/ui/Modal/Modal'
import { Table, type TableColumn } from '@/components/ui/Table/Table'
import { useToast } from '@/components/ui/Toast/useToast'
import { useProject } from '@/features/projects/useProject'
import { ResourceFormModal } from '@/features/resources/components/ResourceFormModal'
import {
  resourceTypeLabels,
  resourceTypes,
} from '@/features/resources/resourceModel'
import {
  useDeleteResource,
  useResources,
} from '@/features/resources/resourceQueries'
import type {
  ProjectResource,
  ResourceFilters,
} from '@/features/resources/types'
import { TaskSelectField } from '@/features/tasks/components/TaskSelectField'
import { memberInitials } from '@/features/tasks/taskBoardModel'
import { useTaskOptions, useTasks } from '@/features/tasks/taskQueries'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

const typeIcons = {
  GoogleDocs: FileText,
  GoogleDrive: HardDrive,
  Canva: Palette,
  GitHub: GitBranch,
  Demo: MonitorPlay,
  Other: Box,
}

function readType(value: string | null) {
  return resourceTypes.find((type) => type === value)
}

function formatUpdated(value: string) {
  return new Intl.DateTimeFormat('en', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date(value))
}

export function ResourceHubPage() {
  const { currentWorkspace } = useWorkspace()
  const { currentProject } = useProject()
  const { showToast } = useToast()
  const [searchParams, setSearchParams] = useSearchParams()
  const [selectedId, setSelectedId] = useState<string>()
  const [isFormOpen, setIsFormOpen] = useState(false)
  const [editingResource, setEditingResource] = useState<ProjectResource>()
  const [deletingResource, setDeletingResource] = useState<ProjectResource>()
  const workspaceId = currentWorkspace!.id
  const projectId = currentProject!.id
  const canManage = currentWorkspace!.role === 'Leader'
  const filters = useMemo<ResourceFilters>(
    () => ({
      search: searchParams.get('search')?.trim() || undefined,
      type: readType(searchParams.get('type')),
    }),
    [searchParams],
  )
  const resourcesQuery = useResources(workspaceId, projectId, filters)
  const taskOptions = useTaskOptions(workspaceId, projectId, canManage)
  const tasksQuery = useTasks(workspaceId, projectId, {}, canManage)
  const resources = resourcesQuery.data ?? []
  const selectedResource = resources.find(
    (resource) => resource.id === selectedId,
  )
  const deleteResource = useDeleteResource(
    workspaceId,
    projectId,
    deletingResource?.id ?? '',
  )
  const hasFilters = Boolean(filters.search || filters.type)

  function setFilter(name: string, value: string) {
    setSearchParams(
      (current) => {
        const next = new URLSearchParams(current)
        if (value) next.set(name, value)
        else next.delete(name)
        return next
      },
      { replace: true },
    )
  }

  function openCreate() {
    setEditingResource(undefined)
    setIsFormOpen(true)
  }

  function openEdit(resource: ProjectResource) {
    setEditingResource(resource)
    setIsFormOpen(true)
  }

  async function copyLink(resource: ProjectResource) {
    try {
      await navigator.clipboard.writeText(resource.url)
      showToast({
        title: 'Link copied',
        description: `${resource.title} is ready to paste.`,
        variant: 'success',
      })
    } catch {
      showToast({
        title: 'Link was not copied',
        description:
          'Your browser blocked clipboard access. Open the resource instead.',
        variant: 'error',
      })
    }
  }

  async function confirmDelete() {
    if (!deletingResource) return
    try {
      await deleteResource.mutateAsync()
      if (selectedId === deletingResource.id) setSelectedId(undefined)
      showToast({
        title: 'Resource removed',
        description:
          'Only TeamPulse metadata was deleted. The external file is unchanged.',
        variant: 'success',
      })
      setDeletingResource(undefined)
    } catch {
      showToast({
        title: 'Resource was not removed',
        description: 'Please retry when the API is available.',
        variant: 'error',
      })
    }
  }

  const columns: TableColumn<ProjectResource>[] = [
    {
      key: 'title',
      header: 'Resource',
      width: '34%',
      cell: (resource) => {
        const Icon = typeIcons[resource.type]
        return (
          <div className="resource-name-cell">
            <span
              className={`resource-type-icon is-${resource.type.toLowerCase()}`}
            >
              <Icon aria-hidden="true" size={17} />
            </span>
            <div>
              <strong>{resource.title}</strong>
              <span>{resource.externalId ?? resource.url}</span>
            </div>
          </div>
        )
      },
    },
    {
      key: 'type',
      header: 'Type',
      cell: (resource) => (
        <span className="resource-type-chip">
          {resourceTypeLabels[resource.type]}
        </span>
      ),
    },
    {
      key: 'task',
      header: 'Linked task',
      cell: (resource) =>
        resource.linkedTask ? (
          <span className="resource-task-link">{resource.linkedTask.code}</span>
        ) : (
          <span className="resource-muted">Not linked</span>
        ),
    },
    {
      key: 'owner',
      header: 'Owner',
      cell: (resource) => (
        <span className="resource-owner">
          <i>{memberInitials(resource.owner.displayName)}</i>
          {resource.owner.displayName}
        </span>
      ),
    },
    {
      key: 'updated',
      header: 'Updated',
      cell: (resource) => formatUpdated(resource.updatedAtUtc),
    },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      align: 'end',
      isAction: true,
      cell: (resource) => (
        <div className="resource-row-actions">
          <Button
            iconOnly
            size="sm"
            variant="ghost"
            aria-label={`Copy link for ${resource.title}`}
            title="Copy link"
            onClick={() => void copyLink(resource)}
          >
            <Copy size={16} />
          </Button>
          <a
            className="ui-button ui-button--ghost ui-button--sm ui-button--icon-only"
            href={resource.url}
            target="_blank"
            rel="noreferrer"
            aria-label={`Open ${resource.title} externally`}
            title="Open external"
          >
            <ExternalLink size={16} />
          </a>
        </div>
      ),
    },
  ]
  const SelectedResourceIcon = selectedResource
    ? typeIcons[selectedResource.type]
    : Box

  return (
    <div className="resource-page">
      <header className="resource-header">
        <div>
          <p className="eyebrow">{currentProject!.name}</p>
          <h1>Resource hub</h1>
          <p>
            Organize links and ownership while original content stays in its
            external service.
          </p>
        </div>
        {canManage && (
          <Button
            leadingIcon={<Plus size={17} />}
            disabled={taskOptions.members.isLoading || tasksQuery.isLoading}
            onClick={openCreate}
          >
            Add resource
          </Button>
        )}
      </header>

      <section className="resource-toolbar" aria-label="Resource filters">
        <Input
          label="Search resources"
          placeholder="Title or external ID"
          startIcon={<Search size={17} />}
          value={searchParams.get('search') ?? ''}
          onChange={(event) => setFilter('search', event.target.value)}
        />
        <TaskSelectField
          label="Type"
          name="resource-filter-type"
          value={filters.type ?? ''}
          onChange={(event) => setFilter('type', event.target.value)}
        >
          <option value="">All types</option>
          {resourceTypes.map((type) => (
            <option key={type} value={type}>
              {resourceTypeLabels[type]}
            </option>
          ))}
        </TaskSelectField>
        {hasFilters && (
          <Button variant="ghost" onClick={() => setSearchParams({})}>
            Clear filters
          </Button>
        )}
      </section>

      {resourcesQuery.isError ? (
        <section className="resource-error" role="alert">
          <RefreshCw aria-hidden="true" size={28} />
          <h2>We could not load project resources.</h2>
          <p>Your filters are safe. Retry when the API is available.</p>
          <Button onClick={() => void resourcesQuery.refetch()}>
            Try again
          </Button>
        </section>
      ) : (
        <div
          className={`resource-layout${selectedResource ? ' has-selection' : ''}`}
        >
          <section className="resource-list-panel">
            <div className="resource-list-summary">
              <strong>
                {resources.length}{' '}
                {resources.length === 1 ? 'resource' : 'resources'}
              </strong>
              <span>
                Metadata only · external content remains authoritative
              </span>
            </div>
            <Table
              columns={columns}
              rows={resources}
              getRowId={(resource) => resource.id}
              getRowLabel={(resource) => `View details for ${resource.title}`}
              caption="Project resources"
              isLoading={resourcesQuery.isLoading}
              onRowClick={(resource) => setSelectedId(resource.id)}
              emptyState={
                <EmptyState
                  icon={
                    hasFilters ? <Search size={24} /> : <FolderOpen size={24} />
                  }
                  title={
                    hasFilters
                      ? 'No resources match these filters'
                      : 'No resources yet'
                  }
                  description={
                    hasFilters
                      ? 'Clear search or type filters to return to the full hub.'
                      : canManage
                        ? 'Add the first document, repository, design or demo link.'
                        : 'A workspace leader has not added project resources yet.'
                  }
                  variant={hasFilters ? 'filtered' : 'first-use'}
                  primaryAction={
                    hasFilters ? (
                      <Button
                        variant="secondary"
                        onClick={() => setSearchParams({})}
                      >
                        Clear filters
                      </Button>
                    ) : canManage ? (
                      <Button
                        leadingIcon={<Plus size={17} />}
                        onClick={openCreate}
                      >
                        Add resource
                      </Button>
                    ) : undefined
                  }
                />
              }
              renderMobileCard={(resource) => {
                const Icon = typeIcons[resource.type]
                return (
                  <div className="resource-mobile-card">
                    <div className="resource-mobile-card__heading">
                      <span
                        className={`resource-type-icon is-${resource.type.toLowerCase()}`}
                      >
                        <Icon size={17} />
                      </span>
                      <div>
                        <strong>{resource.title}</strong>
                        <span>{resourceTypeLabels[resource.type]}</span>
                      </div>
                    </div>
                    <div className="resource-mobile-card__actions">
                      <Button
                        size="sm"
                        variant="secondary"
                        leadingIcon={<Copy size={15} />}
                        onClick={(event) => {
                          event.stopPropagation()
                          void copyLink(resource)
                        }}
                      >
                        Copy
                      </Button>
                      <a
                        className="ui-button ui-button--primary ui-button--sm"
                        href={resource.url}
                        target="_blank"
                        rel="noreferrer"
                        onClick={(event) => event.stopPropagation()}
                      >
                        <ExternalLink size={15} />
                        Open
                      </a>
                    </div>
                  </div>
                )
              }}
            />
          </section>

          {selectedResource && (
            <aside className="resource-detail" aria-label="Resource details">
              <div className="resource-detail__heading">
                <span
                  className={`resource-type-icon is-${selectedResource.type.toLowerCase()}`}
                >
                  <SelectedResourceIcon size={20} />
                </span>
                <div>
                  <p className="eyebrow">
                    {resourceTypeLabels[selectedResource.type]}
                  </p>
                  <h2>{selectedResource.title}</h2>
                </div>
              </div>
              <dl>
                <div>
                  <dt>Owner</dt>
                  <dd>{selectedResource.owner.displayName}</dd>
                </div>
                <div>
                  <dt>Linked task</dt>
                  <dd>
                    {selectedResource.linkedTask
                      ? `${selectedResource.linkedTask.code} · ${selectedResource.linkedTask.title}`
                      : 'Not linked'}
                  </dd>
                </div>
                <div>
                  <dt>External ID</dt>
                  <dd>{selectedResource.externalId ?? 'Not provided'}</dd>
                </div>
                <div>
                  <dt>Updated</dt>
                  <dd>{formatUpdated(selectedResource.updatedAtUtc)}</dd>
                </div>
              </dl>
              <div className="resource-detail__actions">
                <a
                  className="ui-button ui-button--primary ui-button--md"
                  href={selectedResource.url}
                  target="_blank"
                  rel="noreferrer"
                >
                  <ExternalLink size={16} />
                  Open external
                </a>
                <Button
                  variant="secondary"
                  leadingIcon={<Copy size={16} />}
                  onClick={() => void copyLink(selectedResource)}
                >
                  Copy link
                </Button>
              </div>
              {canManage && (
                <div className="resource-detail__manage">
                  <Button
                    variant="ghost"
                    leadingIcon={<Pencil size={16} />}
                    onClick={() => openEdit(selectedResource)}
                  >
                    Edit metadata
                  </Button>
                  <Button
                    variant="danger"
                    leadingIcon={<Trash2 size={16} />}
                    onClick={() => setDeletingResource(selectedResource)}
                  >
                    Remove
                  </Button>
                </div>
              )}
            </aside>
          )}
        </div>
      )}

      {canManage && (
        <ResourceFormModal
          isOpen={isFormOpen}
          workspaceId={workspaceId}
          projectId={projectId}
          resource={editingResource}
          members={taskOptions.members.data ?? []}
          tasks={tasksQuery.data ?? []}
          onClose={() => setIsFormOpen(false)}
          onSaved={(resource) => setSelectedId(resource.id)}
        />
      )}

      <Modal
        isOpen={Boolean(deletingResource)}
        onClose={() => setDeletingResource(undefined)}
        title="Remove resource metadata?"
        description="This removes only the TeamPulse reference. The external document or repository will not be deleted."
        size="sm"
        isPending={deleteResource.isPending}
        footer={
          <>
            <Button
              variant="secondary"
              disabled={deleteResource.isPending}
              onClick={() => setDeletingResource(undefined)}
            >
              Cancel
            </Button>
            <Button
              variant="danger"
              isLoading={deleteResource.isPending}
              loadingLabel="Removing resource"
              onClick={() => void confirmDelete()}
            >
              Remove
            </Button>
          </>
        }
      >
        <p>
          <strong>{deletingResource?.title}</strong> can be added again later
          using its external link.
        </p>
      </Modal>
    </div>
  )
}
