import { useEffect, useRef, useState, type ReactNode } from 'react'
import { FolderKanban, ListTodo, Search, Users } from 'lucide-react'
import { useNavigate } from 'react-router-dom'

import { Input } from '@/components/ui/Input/Input'
import { Modal } from '@/components/ui/Modal/Modal'
import { useAuth } from '@/features/auth/useAuth'
import { projectPath } from '@/features/projects/projectPaths'
import {
  searchApi,
  type WorkspaceSearchResults,
} from '@/features/shell/shellApi'
import { useWorkspace } from '@/features/workspaces/useWorkspace'

interface WorkspaceSearchDialogProps {
  isOpen: boolean
  onClose: () => void
}

const emptyResults: WorkspaceSearchResults = {
  projects: [],
  tasks: [],
  members: [],
}

export function WorkspaceSearchDialog({
  isOpen,
  onClose,
}: WorkspaceSearchDialogProps) {
  const { accessToken } = useAuth()
  const { currentWorkspace } = useWorkspace()
  const navigate = useNavigate()
  const inputRef = useRef<HTMLInputElement>(null)
  const [query, setQuery] = useState('')
  const [results, setResults] = useState(emptyResults)
  const [isSearching, setIsSearching] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const normalizedQuery = query.trim()
  const hasResults =
    results.projects.length + results.tasks.length + results.members.length > 0

  useEffect(() => {
    if (!isOpen) {
      setQuery('')
      setResults(emptyResults)
      setError(null)
      return
    }

    if (!accessToken || !currentWorkspace || normalizedQuery.length < 2) {
      setResults(emptyResults)
      setError(null)
      setIsSearching(false)
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(() => {
      setIsSearching(true)
      setError(null)
      void searchApi
        .search(
          accessToken,
          currentWorkspace.id,
          normalizedQuery,
          controller.signal,
        )
        .then(setResults)
        .catch((requestError: unknown) => {
          if (
            requestError instanceof DOMException &&
            requestError.name === 'AbortError'
          ) {
            return
          }
          setResults(emptyResults)
          setError('Search is unavailable right now. Please try again.')
        })
        .finally(() => setIsSearching(false))
    }, 250)

    return () => {
      window.clearTimeout(timeout)
      controller.abort()
    }
  }, [accessToken, currentWorkspace, isOpen, normalizedQuery])

  function openResult(path: string) {
    onClose()
    navigate(path)
  }

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Search TeamPulse"
      description={`Find projects, tasks and members in ${currentWorkspace?.name ?? 'the current workspace'}.`}
      size="lg"
      initialFocusRef={inputRef}
    >
      <div className="global-search">
        <Input
          ref={inputRef}
          label="Search"
          value={query}
          placeholder="Try a project name, task code or member email"
          startIcon={<Search size={18} />}
          isSearching={isSearching}
          helperText="Enter at least 2 characters. Results stay inside the current workspace."
          onChange={(event) => setQuery(event.target.value)}
        />

        <div className="global-search__results" aria-live="polite">
          {error && <p className="global-search__message is-error">{error}</p>}
          {!error && normalizedQuery.length < 2 && (
            <p className="global-search__message">
              Search by project, task title or code, member name or email.
            </p>
          )}
          {!error &&
            normalizedQuery.length >= 2 &&
            !isSearching &&
            !hasResults && (
              <p className="global-search__message">
                No results found for “{normalizedQuery}”.
              </p>
            )}

          {results.projects.length > 0 && (
            <SearchGroup title="Projects">
              {results.projects.map((project) => (
                <button
                  className="global-search__result"
                  type="button"
                  key={project.id}
                  onClick={() =>
                    openResult(projectPath(currentWorkspace!.id, project.id))
                  }
                >
                  <FolderKanban aria-hidden="true" size={18} />
                  <span>
                    <strong>{project.name}</strong>
                    <small>{project.description || 'Project'}</small>
                  </span>
                </button>
              ))}
            </SearchGroup>
          )}

          {results.tasks.length > 0 && (
            <SearchGroup title="Tasks">
              {results.tasks.map((task) => (
                <button
                  className="global-search__result"
                  type="button"
                  key={task.id}
                  onClick={() =>
                    openResult(
                      `${projectPath(currentWorkspace!.id, task.projectId)}/tasks?task=${task.id}`,
                    )
                  }
                >
                  <ListTodo aria-hidden="true" size={18} />
                  <span>
                    <strong>
                      {task.code} · {task.title}
                    </strong>
                    <small>
                      {task.projectName} · {task.status}
                    </small>
                  </span>
                </button>
              ))}
            </SearchGroup>
          )}

          {results.members.length > 0 && (
            <SearchGroup title="Members">
              {results.members.map((member) => (
                <button
                  className="global-search__result"
                  type="button"
                  key={member.userId}
                  onClick={() =>
                    openResult(`/workspaces/${currentWorkspace!.id}/members`)
                  }
                >
                  <Users aria-hidden="true" size={18} />
                  <span>
                    <strong>{member.displayName}</strong>
                    <small>
                      {member.email} · {member.role}
                    </small>
                  </span>
                </button>
              ))}
            </SearchGroup>
          )}
        </div>
      </div>
    </Modal>
  )
}

function SearchGroup({
  title,
  children,
}: {
  title: string
  children: ReactNode
}) {
  return (
    <section className="global-search__group" aria-label={title}>
      <h3>{title}</h3>
      <div>{children}</div>
    </section>
  )
}
