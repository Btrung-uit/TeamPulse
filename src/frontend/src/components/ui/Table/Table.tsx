import type { CSSProperties, KeyboardEvent, ReactNode } from 'react'
import { ArrowDown, ArrowUp, ChevronsUpDown } from 'lucide-react'

import { classNames } from '@/lib/classNames'

export type SortDirection = 'ascending' | 'descending' | 'none'

export interface TableColumn<T> {
  key: string
  header: ReactNode
  cell: (row: T) => ReactNode
  mobileLabel?: string
  width?: string
  align?: 'start' | 'center' | 'end'
  sortable?: boolean
  sortDirection?: SortDirection
  onSort?: () => void
  isAction?: boolean
}

export interface TableProps<T> {
  columns: TableColumn<T>[]
  rows: T[]
  getRowId: (row: T) => string
  getRowLabel?: (row: T) => string
  caption: string
  emptyState?: ReactNode
  isLoading?: boolean
  onRowClick?: (row: T) => void
  renderMobileCard?: (row: T) => ReactNode
}

function SortIcon({ direction }: { direction: SortDirection }) {
  if (direction === 'ascending') return <ArrowUp aria-hidden="true" size={14} />
  if (direction === 'descending') {
    return <ArrowDown aria-hidden="true" size={14} />
  }
  return <ChevronsUpDown aria-hidden="true" size={14} />
}

export function Table<T>({
  columns,
  rows,
  getRowId,
  getRowLabel,
  caption,
  emptyState,
  isLoading = false,
  onRowClick,
  renderMobileCard,
}: TableProps<T>) {
  function handleRowKeyDown(event: KeyboardEvent, row: T) {
    if (!onRowClick || (event.key !== 'Enter' && event.key !== ' ')) return
    event.preventDefault()
    onRowClick(row)
  }

  if (!isLoading && rows.length === 0) {
    return (
      <div className="ui-table-empty">
        {emptyState ?? (
          <div className="ui-table-empty__default">
            <strong>No data yet</strong>
            <span>Items will appear here when they are available.</span>
          </div>
        )}
      </div>
    )
  }

  return (
    <div className="ui-table-responsive">
      <div className="ui-table-container">
        <table className="ui-table">
          <caption className="sr-only">{caption}</caption>
          <thead>
            <tr>
              {columns.map((column) => (
                <th
                  key={column.key}
                  scope="col"
                  aria-sort={
                    column.sortable
                      ? (column.sortDirection ?? 'none')
                      : undefined
                  }
                  className={`is-${column.align ?? 'start'}`}
                  style={
                    column.width
                      ? ({ '--column-width': column.width } as CSSProperties)
                      : undefined
                  }
                >
                  {column.sortable ? (
                    <button type="button" onClick={column.onSort}>
                      {column.header}
                      <SortIcon direction={column.sortDirection ?? 'none'} />
                    </button>
                  ) : (
                    column.header
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading
              ? Array.from({ length: 4 }, (_, rowIndex) => (
                  <tr className="ui-table__skeleton" key={rowIndex}>
                    {columns.map((column) => (
                      <td key={column.key}>
                        <span />
                      </td>
                    ))}
                  </tr>
                ))
              : rows.map((row) => (
                  <tr
                    key={getRowId(row)}
                    className={classNames(onRowClick && 'is-interactive')}
                    tabIndex={onRowClick ? 0 : undefined}
                    aria-label={getRowLabel?.(row)}
                    onClick={() => onRowClick?.(row)}
                    onKeyDown={(event) => handleRowKeyDown(event, row)}
                  >
                    {columns.map((column) => (
                      <td
                        key={column.key}
                        className={`is-${column.align ?? 'start'}`}
                        onClick={
                          column.isAction
                            ? (event) => event.stopPropagation()
                            : undefined
                        }
                        onKeyDown={
                          column.isAction
                            ? (event) => event.stopPropagation()
                            : undefined
                        }
                      >
                        {column.cell(row)}
                      </td>
                    ))}
                  </tr>
                ))}
          </tbody>
        </table>
      </div>

      <div className="ui-table-mobile" aria-label={`${caption}, mobile view`}>
        {isLoading
          ? Array.from({ length: 3 }, (_, index) => (
              <div className="ui-table-mobile__skeleton" key={index} />
            ))
          : rows.map((row) => (
              <article
                className={classNames(
                  'ui-table-mobile__card',
                  onRowClick && 'is-interactive',
                )}
                key={getRowId(row)}
                role={onRowClick ? 'button' : undefined}
                tabIndex={onRowClick ? 0 : undefined}
                aria-label={getRowLabel?.(row)}
                onClick={() => onRowClick?.(row)}
                onKeyDown={(event) => handleRowKeyDown(event, row)}
              >
                {renderMobileCard
                  ? renderMobileCard(row)
                  : columns
                      .filter((column) => !column.isAction)
                      .map((column) => (
                        <div key={column.key}>
                          <span>{column.mobileLabel ?? column.header}</span>
                          <span className="ui-table-mobile__value">
                            {column.cell(row)}
                          </span>
                        </div>
                      ))}
              </article>
            ))}
      </div>
    </div>
  )
}
