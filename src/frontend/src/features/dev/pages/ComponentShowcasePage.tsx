import { useState } from 'react'
import {
  Archive,
  ArrowRight,
  FolderSearch,
  MoreHorizontal,
  Plus,
  Save,
  Search,
  Trash2,
} from 'lucide-react'

import {
  Button,
  EmptyState,
  Input,
  Modal,
  StatusBadge,
  Table,
  TaskCard,
  Textarea,
  type SortDirection,
  type TableColumn,
  type TaskStatus,
} from '@/components/ui'
import { useToast } from '@/components/ui/Toast/useToast'

interface DemoTask {
  id: string
  code: string
  title: string
  owner: string
  status: TaskStatus
  due: string
}

const demoRows: DemoTask[] = [
  {
    id: '1',
    code: 'TP-021',
    title: 'Build project creation flow',
    owner: 'Bảo Trung',
    status: 'in-progress',
    due: 'Oct 18',
  },
  {
    id: '2',
    code: 'TP-022',
    title: 'Review workspace permission matrix',
    owner: 'Minh Anh',
    status: 'in-review',
    due: 'Oct 19',
  },
  {
    id: '3',
    code: 'TP-023',
    title: 'Attach demo evidence',
    owner: 'Hoàng Nam',
    status: 'done',
    due: 'Oct 20',
  },
]

function ComponentShowcasePage() {
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [sortDirection, setSortDirection] = useState<SortDirection>('ascending')
  const { showToast } = useToast()

  const sortedRows = [...demoRows].sort((left, right) => {
    const comparison = left.title.localeCompare(right.title)
    return sortDirection === 'descending' ? -comparison : comparison
  })

  const columns: TableColumn<DemoTask>[] = [
    {
      key: 'task',
      header: 'Task',
      mobileLabel: 'Task',
      sortable: true,
      sortDirection,
      onSort: () =>
        setSortDirection((current) =>
          current === 'ascending' ? 'descending' : 'ascending',
        ),
      cell: (task) => (
        <span className="showcase-table-task">
          <small>{task.code}</small>
          <strong>{task.title}</strong>
        </span>
      ),
    },
    {
      key: 'owner',
      header: 'Owner',
      cell: (task) => task.owner,
    },
    {
      key: 'status',
      header: 'Status',
      cell: (task) => <StatusBadge status={task.status} />,
    },
    {
      key: 'due',
      header: 'Due date',
      cell: (task) => task.due,
    },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      align: 'end',
      isAction: true,
      cell: (task) => (
        <Button
          iconOnly
          variant="ghost"
          size="sm"
          aria-label={`Open actions for ${task.code}`}
          title="Row actions"
          onClick={() =>
            showToast({ title: `${task.code} actions opened`, variant: 'info' })
          }
        >
          <MoreHorizontal aria-hidden="true" size={18} />
        </Button>
      ),
    },
  ]

  return (
    <div className="component-showcase">
      <header className="page-header component-showcase__header">
        <div>
          <p className="eyebrow">Development · TP-006</p>
          <h1>Component primitives</h1>
          <p>
            Reusable, accessible foundations for TeamPulse product features.
          </p>
        </div>
        <StatusBadge status="in-progress" label="8 components" />
      </header>

      <section className="showcase-section" aria-labelledby="buttons-title">
        <div className="showcase-section__heading">
          <div>
            <p className="eyebrow">Actions</p>
            <h2 id="buttons-title">Button</h2>
          </div>
          <span>6 variants · 3 sizes</span>
        </div>
        <div className="showcase-row">
          <Button size="lg" leadingIcon={<Plus size={17} />}>
            Create task
          </Button>
          <Button size="sm" variant="secondary">
            Secondary
          </Button>
          <Button variant="ghost">Ghost</Button>
          <Button variant="danger" leadingIcon={<Trash2 size={17} />}>
            Archive
          </Button>
          <Button variant="link" trailingIcon={<ArrowRight size={16} />}>
            View details
          </Button>
          <Button isLoading>Saving</Button>
          <Button disabled>Disabled</Button>
          <Button
            iconOnly
            variant="secondary"
            aria-label="Save changes"
            title="Save changes"
          >
            <Save aria-hidden="true" size={18} />
          </Button>
        </div>
      </section>

      <section className="showcase-section" aria-labelledby="inputs-title">
        <div className="showcase-section__heading">
          <div>
            <p className="eyebrow">Forms</p>
            <h2 id="inputs-title">Input</h2>
          </div>
          <span>Label · helper · validation</span>
        </div>
        <div className="showcase-field-grid">
          <Input
            label="Task title"
            placeholder="Enter a clear outcome"
            helperText="Use an action verb and a measurable result."
          />
          <Input
            label="Search tasks"
            placeholder="Code or title"
            startIcon={<Search size={17} />}
            isSearching
          />
          <Input
            label="Assignee email"
            defaultValue="invalid-email"
            error="Enter a valid workspace member email."
          />
          <Input label="Workspace" defaultValue="UIT Workspace" readOnly />
          <Textarea
            label="Task description"
            placeholder="Describe the expected result"
            helperText="Markdown will be supported in a later milestone."
          />
          <Input label="Archived project" defaultValue="CampusEvent" disabled />
        </div>
      </section>

      <section className="showcase-section" aria-labelledby="badges-title">
        <div className="showcase-section__heading">
          <div>
            <p className="eyebrow">Workflow</p>
            <h2 id="badges-title">StatusBadge</h2>
          </div>
          <span>Icon + text + semantic tone</span>
        </div>
        <div className="showcase-row">
          {(
            [
              'todo',
              'in-progress',
              'evidence-linked',
              'in-review',
              'needs-changes',
              'blocked',
              'done',
              'archived',
            ] as TaskStatus[]
          ).map((status) => (
            <StatusBadge key={status} status={status} />
          ))}
        </div>
      </section>

      <section className="showcase-section" aria-labelledby="feedback-title">
        <div className="showcase-section__heading">
          <div>
            <p className="eyebrow">Feedback</p>
            <h2 id="feedback-title">Modal and Toast</h2>
          </div>
          <span>Focus managed · live regions</span>
        </div>
        <div className="showcase-row">
          <Button onClick={() => setIsModalOpen(true)}>Open modal</Button>
          <Button
            variant="secondary"
            onClick={() =>
              showToast({
                title: 'Review requested',
                description: 'The project leader has been notified.',
                variant: 'info',
              })
            }
          >
            Info toast
          </Button>
          <Button
            variant="secondary"
            onClick={() =>
              showToast({
                title: 'Task saved',
                description: 'TP-021 was updated successfully.',
                variant: 'success',
              })
            }
          >
            Success toast
          </Button>
          <Button
            variant="secondary"
            onClick={() =>
              showToast({
                title: 'Deadline approaching',
                description: 'Two tasks are due within 48 hours.',
                variant: 'warning',
              })
            }
          >
            Warning toast
          </Button>
          <Button
            variant="secondary"
            onClick={() =>
              showToast({
                title: 'Import failed',
                description: 'Download the error report and try again.',
                variant: 'error',
                action: { label: 'Download report', onClick: () => undefined },
              })
            }
          >
            Actionable error
          </Button>
          <Button
            variant="secondary"
            onClick={() =>
              showToast({
                title: 'Importing tasks',
                description: 'Validating 24 rows…',
                variant: 'progress',
              })
            }
          >
            Progress toast
          </Button>
        </div>
      </section>

      <section className="showcase-section" aria-labelledby="cards-title">
        <div className="showcase-section__heading">
          <div>
            <p className="eyebrow">Task board</p>
            <h2 id="cards-title">TaskCard</h2>
          </div>
          <span>
            Default · selected · risk · dragging · archived · skeleton
          </span>
        </div>
        <div className="showcase-card-grid">
          <TaskCard
            code="TP-021"
            title="Build the project creation flow"
            status="in-progress"
            priority="high"
            assignee={{ name: 'Bảo Trung', initials: 'BT' }}
            dueDate="Oct 18"
            evidence={[
              { type: 'commit', count: 4, label: '4 linked commits' },
              { type: 'attachment', count: 2, label: '2 attachments' },
            ]}
            onClick={() => undefined}
            onMenuClick={() => undefined}
          />
          <TaskCard
            code="TP-022"
            title="Review workspace permission matrix"
            status="in-review"
            priority="medium"
            assignee={{ name: 'Minh Anh', initials: 'MA' }}
            dueDate="Oct 19"
            selected
            onClick={() => undefined}
            onMenuClick={() => undefined}
          />
          <TaskCard
            code="TP-023"
            title="Resolve blocked Excel mapping dependency"
            status="blocked"
            priority="urgent"
            assignee={{ name: 'Hoàng Nam', initials: 'HN' }}
            dueDate="Yesterday"
            blocked
            overdue
          />
          <TaskCard
            code="TP-024"
            title="Prepare contribution report preview"
            status="evidence-linked"
            priority="medium"
            assignee={{ name: 'Gia Hân', initials: 'GH' }}
            dueDate="Oct 21"
            dragging
          />
          <TaskCard
            code="TP-025"
            title="Retired onboarding experiment"
            status="archived"
            priority="low"
            assignee={{ name: 'Bảo Trung', initials: 'BT' }}
            dueDate="Archived"
            archived
          />
          <TaskCard
            code="loading"
            title="Loading task"
            status="todo"
            priority="low"
            assignee={{ name: '', initials: '' }}
            dueDate=""
            skeleton
          />
        </div>
      </section>

      <section className="showcase-section" aria-labelledby="table-title">
        <div className="showcase-section__heading">
          <div>
            <p className="eyebrow">Structured data</p>
            <h2 id="table-title">Table</h2>
          </div>
          <span>Sortable · keyboard rows · mobile cards</span>
        </div>
        <Table
          caption="Project tasks"
          columns={columns}
          rows={sortedRows}
          getRowId={(task) => task.id}
          getRowLabel={(task) => `Open ${task.code}: ${task.title}`}
          onRowClick={(task) =>
            showToast({ title: `${task.code} selected`, variant: 'info' })
          }
        />
      </section>

      <section className="showcase-section" aria-labelledby="empty-title">
        <div className="showcase-section__heading">
          <div>
            <p className="eyebrow">System state</p>
            <h2 id="empty-title">EmptyState</h2>
          </div>
          <span>One clear next action</span>
        </div>
        <EmptyState
          variant="filtered"
          icon={<FolderSearch size={24} />}
          title="No tasks match these filters"
          description="Clear the active filters or adjust your search to see project tasks."
          primaryAction={<Button>Clear filters</Button>}
          secondaryAction={<Button variant="ghost">View all tasks</Button>}
        />
      </section>

      <Modal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        title="Archive project"
        description="Confirm what happens to the project history before continuing."
        footer={
          <>
            <Button variant="secondary" onClick={() => setIsModalOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="danger"
              leadingIcon={<Archive size={17} />}
              onClick={() => {
                setIsModalOpen(false)
                showToast({
                  title: 'Project archived',
                  description: 'History and evidence remain available.',
                  variant: 'success',
                })
              }}
            >
              Archive project
            </Button>
          </>
        }
      >
        <p className="modal-demo-copy">
          Members will lose write access. Tasks, activity history and linked
          evidence remain available in the archive.
        </p>
      </Modal>
    </div>
  )
}

export default ComponentShowcasePage
