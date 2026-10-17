import { describe, expect, it } from 'vitest'

import {
  filterAndSortMyTasks,
  isMyTaskDueSoon,
  isMyTaskOverdue,
  summarizeMyTasks,
} from '@/features/tasks/myTasksModel'
import type { TaskSummary } from '@/features/tasks/types'

function task(overrides: Partial<TaskSummary>): TaskSummary {
  return {
    id: 'task-1',
    code: 'TP-025',
    title: 'Build My Tasks',
    status: 'Todo',
    priority: 'Medium',
    dueOn: null,
    estimateHours: null,
    assignee: null,
    reviewer: null,
    sprint: null,
    isArchived: false,
    updatedAtUtc: '2026-10-17T00:00:00Z',
    rowVersion: 'version-1',
    ...overrides,
  }
}

const today = new Date(2026, 9, 17)

describe('my tasks model', () => {
  it('sorts blocked and overdue work before the normal queue', () => {
    const result = filterAndSortMyTasks(
      [
        task({ id: 'normal', code: 'TP-3', priority: 'Urgent' }),
        task({
          id: 'overdue',
          code: 'TP-2',
          status: 'InProgress',
          dueOn: '2026-10-16',
        }),
        task({
          id: 'blocked',
          code: 'TP-1',
          status: 'Blocked',
          dueOn: '2026-10-24',
        }),
      ],
      { search: '', status: 'active', due: 'all', today },
    )

    expect(result.map((item) => item.id)).toEqual([
      'blocked',
      'overdue',
      'normal',
    ])
  })

  it('filters status, deadline bucket and search without including completed work by default', () => {
    const tasks = [
      task({
        id: 'due',
        code: 'TP-10',
        title: 'Mobile queue',
        dueOn: '2026-10-20',
      }),
      task({
        id: 'later',
        code: 'TP-11',
        title: 'Later work',
        dueOn: '2026-11-01',
      }),
      task({
        id: 'done',
        code: 'TP-12',
        title: 'Mobile done',
        status: 'Done',
        dueOn: '2026-10-20',
      }),
    ]

    expect(
      filterAndSortMyTasks(tasks, {
        search: 'mobile',
        status: 'active',
        due: 'next-7-days',
        today,
      }).map((item) => item.id),
    ).toEqual(['due'])
    expect(
      filterAndSortMyTasks(tasks, {
        search: '',
        status: 'Done',
        due: 'all',
        today,
      }).map((item) => item.id),
    ).toEqual(['done'])
  })

  it('calculates non-overlapping overdue and due-soon monitoring counts', () => {
    const tasks = [
      task({ id: 'overdue', dueOn: '2026-10-16' }),
      task({ id: 'soon', dueOn: '2026-10-20' }),
      task({ id: 'blocked', status: 'Blocked' }),
      task({ id: 'done', status: 'Done', dueOn: '2026-10-16' }),
    ]

    expect(isMyTaskOverdue(tasks[0], today)).toBe(true)
    expect(isMyTaskDueSoon(tasks[1], today)).toBe(true)
    expect(summarizeMyTasks(tasks, today)).toEqual({
      active: 3,
      blocked: 1,
      overdue: 1,
      dueSoon: 1,
    })
  })
})
