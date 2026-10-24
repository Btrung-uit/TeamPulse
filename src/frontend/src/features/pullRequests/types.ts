export type PullRequestFilter = 'all' | 'waiting' | 'changes' | 'merged'

export type PullRequestStatus =
  | 'Draft'
  | 'WaitingForReview'
  | 'ChangesRequested'
  | 'ReadyToMerge'
  | 'Merged'
  | 'Closed'

export type PullRequestCheckStatus =
  'Unknown' | 'Pending' | 'Passing' | 'Failing'

export interface PullRequestReviewer {
  userId: string
  displayName: string
  email: string
}

export interface PullRequestTask {
  id: string
  code: string
  title: string
  reviewer: PullRequestReviewer | null
}

export interface PullRequestItem {
  id: string
  repositoryFullName: string
  number: number
  title: string
  url: string
  authorLogin: string | null
  requestedReviewers: string[]
  status: PullRequestStatus
  checkStatus: PullRequestCheckStatus
  openedAtUtc: string
  lastActivityAtUtc: string
  tasks: PullRequestTask[]
}

export interface PullRequestFeed {
  items: PullRequestItem[]
  waitingCount: number
  changesCount: number
  mergedCount: number
}
