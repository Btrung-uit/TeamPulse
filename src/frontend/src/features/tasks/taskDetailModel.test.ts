import { describe, expect, it } from 'vitest'

import { allowedStatusTransitions } from '@/features/tasks/taskDetailModel'
import type { TaskDetails } from '@/features/tasks/types'

const task: TaskDetails = {
  id: 'task-1',
  workspaceId: 'workspace-1',
  projectId: 'project-1',
  code: 'TP-024',
  title: 'Build task detail',
  description: null,
  status: 'InReview',
  statusBeforeBlocked: null,
  priority: 'High',
  dueOn: null,
  estimateHours: null,
  assignee: {
    userId: 'assignee-1',
    displayName: 'Assignee',
    email: 'assignee@example.com',
  },
  reviewer: {
    userId: 'reviewer-1',
    displayName: 'Reviewer',
    email: 'reviewer@example.com',
  },
  sprint: null,
  isArchived: false,
  archivedAtUtc: null,
  createdAtUtc: '2026-10-17T00:00:00Z',
  updatedAtUtc: '2026-10-17T00:00:00Z',
  rowVersion: 'version-1',
}

describe('task detail permissions', () => {
  it('lets a leader use every valid domain transition', () => {
    expect(allowedStatusTransitions(task, 'Leader', 'leader-1')).toEqual([
      'InProgress',
      'NeedsChanges',
      'Done',
      'Blocked',
    ])
  })

  it('separates assignee and reviewer transitions', () => {
    expect(allowedStatusTransitions(task, 'Member', 'assignee-1')).toEqual([
      'InProgress',
      'Blocked',
    ])
    expect(allowedStatusTransitions(task, 'Member', 'reviewer-1')).toEqual([
      'NeedsChanges',
    ])
    expect(allowedStatusTransitions(task, 'Advisor', 'reviewer-1')).toEqual([])
  })

  it('offers only the stored recovery status while blocked', () => {
    expect(
      allowedStatusTransitions(
        {
          ...task,
          status: 'Blocked',
          statusBeforeBlocked: 'InReview',
        },
        'Leader',
        'leader-1',
      ),
    ).toEqual(['InReview'])
  })
})
