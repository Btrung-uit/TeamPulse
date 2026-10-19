import { useEffect, useMemo, useState, type FormEvent } from 'react'
import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  FileSpreadsheet,
  Upload,
} from 'lucide-react'
import { Link, useParams } from 'react-router-dom'

import { Button, Modal } from '@/components/ui'
import { useAuth } from '@/features/auth/useAuth'
import { useProject } from '@/features/projects/useProject'
import { taskApi, TaskApiError } from '@/features/tasks/taskApi'
import type {
  ImportColumn,
  TaskImportMapping,
  TaskImportConfirmation,
  TaskImportHistoryItem,
  TaskImportPreview,
  TaskImportUpload,
} from '@/features/tasks/types'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

type MappingKey = keyof TaskImportMapping

const fields: { key: MappingKey; label: string; required: boolean }[] = [
  { key: 'taskCode', label: 'Task code', required: true },
  { key: 'title', label: 'Title', required: true },
  { key: 'assigneeEmail', label: 'Assignee email', required: false },
  { key: 'status', label: 'Status', required: true },
  { key: 'priority', label: 'Priority', required: true },
  { key: 'dueDate', label: 'Due date', required: false },
  { key: 'estimateHours', label: 'Estimate hours', required: false },
  { key: 'dependsOn', label: 'Depends on', required: false },
]

const headerAliases: Record<MappingKey, string[]> = {
  taskCode: ['taskcode', 'task code', 'code'],
  title: ['title', 'task title'],
  assigneeEmail: ['assigneeemail', 'assignee email', 'email'],
  status: ['status'],
  priority: ['priority'],
  dueDate: ['duedate', 'due date'],
  estimateHours: ['estimatehours', 'estimate hours', 'estimate'],
  dependsOn: ['dependson', 'depends on', 'dependency'],
}

function autoMap(columns: ImportColumn[]): TaskImportMapping {
  const find = (key: MappingKey) =>
    columns.find((column) =>
      headerAliases[key].includes(column.name.trim().toLowerCase()),
    )?.number ?? null
  return {
    taskCode: find('taskCode') ?? 0,
    title: find('title') ?? 0,
    assigneeEmail: find('assigneeEmail'),
    status: find('status') ?? 0,
    priority: find('priority') ?? 0,
    dueDate: find('dueDate'),
    estimateHours: find('estimateHours'),
    dependsOn: find('dependsOn'),
  }
}

export function TaskImportPage() {
  const { workspaceId = '', projectId = '' } = useParams()
  const { accessToken } = useAuth()
  const { currentWorkspace } = useWorkspace()
  const { currentProject } = useProject()
  const [file, setFile] = useState<File | null>(null)
  const [upload, setUpload] = useState<TaskImportUpload | null>(null)
  const [sheetName, setSheetName] = useState('')
  const [mapping, setMapping] = useState<TaskImportMapping | null>(null)
  const [preview, setPreview] = useState<TaskImportPreview | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [confirmation, setConfirmation] =
    useState<TaskImportConfirmation | null>(null)
  const [history, setHistory] = useState<TaskImportHistoryItem[]>([])

  const sheet = useMemo(
    () => upload?.sheets.find((candidate) => candidate.name === sheetName),
    [sheetName, upload],
  )

  useEffect(() => {
    if (!accessToken || currentWorkspace?.role !== 'Leader') return
    let active = true
    void taskApi
      .listImportHistory(accessToken, workspaceId, projectId)
      .then((items) => {
        if (active) setHistory(items)
      })
      .catch(() => undefined)
    return () => {
      active = false
    }
  }, [accessToken, currentWorkspace?.role, projectId, workspaceId])

  if (currentWorkspace?.role !== 'Leader') {
    return (
      <section className="import-state" role="alert">
        <p className="eyebrow">Leader access required</p>
        <h1>Only workspace leaders can import tasks.</h1>
        <p>Ask a leader to upload and validate the workbook.</p>
        <Link
          className="ui-button ui-button--secondary ui-button--md"
          to="../tasks"
        >
          Back to task board
        </Link>
      </section>
    )
  }

  async function handleUpload(event: FormEvent) {
    event.preventDefault()
    if (!file || !accessToken) return
    setBusy(true)
    setError(null)
    setPreview(null)
    setConfirmation(null)
    try {
      const result = await taskApi.uploadImport(
        accessToken,
        workspaceId,
        projectId,
        file,
      )
      const firstSheet = result.sheets[0]
      setUpload(result)
      setSheetName(firstSheet.name)
      setMapping(autoMap(firstSheet.columns))
      setHistory(
        await taskApi.listImportHistory(accessToken, workspaceId, projectId),
      )
    } catch (reason) {
      setError(
        reason instanceof TaskApiError
          ? reason.message
          : 'Unable to upload the workbook.',
      )
    } finally {
      setBusy(false)
    }
  }

  function selectSheet(name: string) {
    const selected = upload!.sheets.find(
      (candidate) => candidate.name === name,
    )!
    setSheetName(name)
    setMapping(autoMap(selected.columns))
    setPreview(null)
  }

  async function handlePreview(event: FormEvent) {
    event.preventDefault()
    if (!accessToken || !upload || !mapping) return
    setBusy(true)
    setError(null)
    try {
      const result = await taskApi.previewImport(
        accessToken,
        workspaceId,
        projectId,
        upload.batchId,
        sheetName,
        mapping,
      )
      setPreview(result)
      setHistory(
        await taskApi.listImportHistory(accessToken, workspaceId, projectId),
      )
    } catch (reason) {
      setError(
        reason instanceof TaskApiError
          ? reason.message
          : 'Unable to validate the mapped rows.',
      )
    } finally {
      setBusy(false)
    }
  }

  async function handleConfirm() {
    if (!accessToken || !upload) return
    setBusy(true)
    setError(null)
    try {
      const result = await taskApi.confirmImport(
        accessToken,
        workspaceId,
        projectId,
        upload.batchId,
      )
      setConfirmation(result)
      setConfirmOpen(false)
      setHistory(
        await taskApi.listImportHistory(accessToken, workspaceId, projectId),
      )
    } catch (reason) {
      setConfirmOpen(false)
      setError(
        reason instanceof TaskApiError
          ? reason.message
          : 'Unable to confirm the import.',
      )
    } finally {
      setBusy(false)
    }
  }

  const requiredMapped =
    mapping &&
    mapping.taskCode > 0 &&
    mapping.title > 0 &&
    mapping.status > 0 &&
    mapping.priority > 0

  return (
    <main className="task-import-page">
      <header className="task-board-header">
        <div>
          <p className="eyebrow">Excel onboarding</p>
          <h1>Import tasks into {currentProject?.name}</h1>
          <p>Upload, map and fix errors before any task is created.</p>
        </div>
        <Link
          className="ui-button ui-button--secondary ui-button--md"
          to="../tasks"
        >
          Back to board
        </Link>
      </header>

      <ol className="import-steps" aria-label="Import progress">
        {['Upload', 'Map columns', 'Preview errors'].map((label, index) => (
          <li
            className={
              index <= (preview ? 2 : upload ? 1 : 0) ? 'is-active' : ''
            }
            key={label}
          >
            <span>{index + 1}</span>
            {label}
          </li>
        ))}
      </ol>

      {error && (
        <p className="task-detail-error" role="alert">
          {error}
        </p>
      )}

      <section className="import-panel">
        <div>
          <FileSpreadsheet size={22} aria-hidden="true" />
          <div>
            <h2>1. Upload workbook</h2>
            <p>.xlsx only, maximum 5 MB.</p>
          </div>
        </div>
        <form className="import-upload" onSubmit={handleUpload}>
          <input
            aria-label="Excel workbook"
            accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            type="file"
            onChange={(event) => setFile(event.target.files?.[0] ?? null)}
          />
          <Button
            type="submit"
            disabled={!file}
            isLoading={busy && !upload}
            leadingIcon={<Upload size={17} />}
          >
            Upload
          </Button>
        </form>
        {upload && (
          <p className="import-file-summary">
            <strong>{upload.fileName}</strong> ·{' '}
            {(upload.fileSizeBytes / 1024).toFixed(1)} KB ·{' '}
            {upload.sheets.length} sheet(s)
          </p>
        )}
      </section>

      {upload && mapping && sheet && (
        <section className="import-panel">
          <div>
            <div>
              <h2>2. Select sheet and map columns</h2>
              <p>Required fields are marked with an asterisk.</p>
            </div>
          </div>
          <form className="import-mapping" onSubmit={handlePreview}>
            <label>
              Sheet
              <select
                value={sheetName}
                onChange={(event) => selectSheet(event.target.value)}
              >
                {upload.sheets.map((item) => (
                  <option value={item.name} key={item.name}>
                    {item.name} ({item.dataRowCount} rows)
                  </option>
                ))}
              </select>
            </label>
            <div className="import-mapping-grid">
              {fields.map((field) => (
                <label key={field.key}>
                  {field.label}
                  {field.required ? ' *' : ''}
                  <select
                    value={mapping[field.key] ?? ''}
                    onChange={(event) =>
                      setMapping({
                        ...mapping,
                        [field.key]: event.target.value
                          ? Number(event.target.value)
                          : field.required
                            ? 0
                            : null,
                      })
                    }
                  >
                    <option value="">Not mapped</option>
                    {sheet.columns.map((column) => (
                      <option value={column.number} key={column.number}>
                        {column.name} (column {column.number})
                      </option>
                    ))}
                  </select>
                </label>
              ))}
            </div>
            <Button type="submit" disabled={!requiredMapped} isLoading={busy}>
              Validate preview
            </Button>
          </form>
        </section>
      )}

      {preview && (
        <section className="import-panel import-preview">
          <div>
            {preview.errorRows === 0 ? (
              <CheckCircle2 className="is-success" size={22} />
            ) : (
              <AlertTriangle className="is-warning" size={22} />
            )}
            <div>
              <h2>3. Validation preview</h2>
              <p>
                {preview.validRows} valid · {preview.errorRows} invalid ·{' '}
                {preview.totalRows} total
              </p>
            </div>
          </div>
          {preview.errorRows === 0 && (
            <div className="import-ready">
              {confirmation ? (
                <p>
                  <strong>
                    {confirmation.importedTaskCount} tasks imported.
                  </strong>{' '}
                  TeamPulse is now the source of truth for this project.
                </p>
              ) : (
                <>
                  <p>
                    All rows are valid. Confirm once to create every task
                    atomically.
                  </p>
                  <Button onClick={() => setConfirmOpen(true)}>
                    Confirm import
                  </Button>
                </>
              )}
            </div>
          )}
          <div className="import-preview-table">
            <table>
              <caption className="sr-only">
                Excel import validation preview
              </caption>
              <thead>
                <tr>
                  <th>Row</th>
                  <th>Task code</th>
                  <th>Title</th>
                  <th>Status</th>
                  <th>Validation</th>
                </tr>
              </thead>
              <tbody>
                {preview.rows.map((row) => (
                  <tr
                    className={row.errors.length ? 'has-error' : ''}
                    key={row.rowNumber}
                  >
                    <td>{row.rowNumber}</td>
                    <td>{row.taskCode || '—'}</td>
                    <td>{row.title || '—'}</td>
                    <td>{row.status || '—'}</td>
                    <td>
                      {row.errors.length === 0 ? (
                        <span className="import-valid">Valid</span>
                      ) : (
                        <ul>
                          {row.errors.map((item, index) => (
                            <li key={`${item.column}-${index}`}>
                              <strong>{item.column}:</strong> {item.reason}
                            </li>
                          ))}
                        </ul>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      <section className="import-panel import-history">
        <div>
          <Clock3 size={22} aria-hidden="true" />
          <div>
            <h2>Recent imports</h2>
            <p>
              The 20 latest upload and confirmation attempts for this project.
            </p>
          </div>
        </div>
        {history.length === 0 ? (
          <p>No import history yet.</p>
        ) : (
          <div className="import-preview-table">
            <table>
              <caption className="sr-only">Recent Excel imports</caption>
              <thead>
                <tr>
                  <th>File</th>
                  <th>Status</th>
                  <th>Rows</th>
                  <th>Uploaded</th>
                </tr>
              </thead>
              <tbody>
                {history.map((item) => (
                  <tr key={item.batchId}>
                    <td>{item.fileName}</td>
                    <td>
                      <span
                        className={`import-status import-status--${item.status.toLowerCase()}`}
                      >
                        {item.status}
                      </span>
                    </td>
                    <td>
                      {item.status === 'Confirmed'
                        ? `${item.importedTaskCount} imported`
                        : `${item.validRows} valid / ${item.errorRows} invalid`}
                    </td>
                    <td>{new Date(item.uploadedAtUtc).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <Modal
        isOpen={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        title="Confirm task import"
        description="This creates all validated tasks and dependencies in one transaction."
        size="sm"
        isPending={busy}
        dismissible={!busy}
        footer={
          <>
            <Button
              variant="secondary"
              disabled={busy}
              onClick={() => setConfirmOpen(false)}
            >
              Cancel
            </Button>
            <Button
              isLoading={busy}
              loadingLabel="Importing tasks"
              onClick={() => void handleConfirm()}
            >
              Import {preview?.validRows ?? 0} tasks
            </Button>
          </>
        }
      >
        <p className="import-confirm-copy">
          Re-uploading this workbook later will create a new batch and will not
          overwrite these tasks. Duplicate task codes will be rejected.
        </p>
      </Modal>
    </main>
  )
}
