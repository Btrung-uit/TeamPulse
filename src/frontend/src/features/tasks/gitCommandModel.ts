export const gitBranchTypes = [
  'feature',
  'fix',
  'docs',
  'test',
  'refactor',
  'chore',
] as const

export const gitCommitTypes = [
  'feat',
  'fix',
  'docs',
  'test',
  'refactor',
  'chore',
  'style',
] as const

export type GitBranchType = (typeof gitBranchTypes)[number]
export type GitCommitType = (typeof gitCommitTypes)[number]

export interface GitCommandInput {
  taskCode: string
  taskTitle: string
  branchType: GitBranchType
  commitType: GitCommitType
  commitSummary: string
  stagePaths: string
}

export interface GitCommand {
  id:
    | 'base'
    | 'pull'
    | 'branch'
    | 'status'
    | 'stage'
    | 'review'
    | 'commit'
    | 'push'
  label: string
  command: string
}

export interface GitCommandPlan {
  branchName: string
  commitMessage: string
  commands: GitCommand[]
  invalidStagePaths: string[]
  needsStagePaths: boolean
}

export function slugifyBranch(value: string) {
  return (
    value
      .replace(/[đĐ]/g, 'd')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '')
      .slice(0, 48)
      .replace(/-+$/g, '') || 'task'
  )
}

export function defaultCommitSummary(taskTitle: string) {
  return `complete ${taskTitle}`.toLowerCase().slice(0, 72).trim()
}

function safeCommitSummary(value: string) {
  return (
    value
      .replace(/["'`$;&|<>!]/g, '')
      .replace(/\s+/g, ' ')
      .trim()
      .slice(0, 72) || 'complete task'
  )
}

function isSafeRelativePath(path: string) {
  if (!path || path === '.' || path === './' || path.startsWith('-'))
    return false
  if (/^[a-zA-Z]:/.test(path) || path.startsWith('/')) return false
  if (path.split('/').includes('..')) return false
  const unsafeCharacters = [
    '"',
    '`',
    '$',
    ';',
    '&',
    '|',
    '<',
    '>',
    '!',
    '*',
    '?',
    '[',
    ']',
    '{',
    '}',
    '(',
    ')',
  ]
  return !unsafeCharacters.some((character) => path.includes(character))
}

export function parseStagePaths(value: string) {
  const candidates = value
    .split(/\r?\n/)
    .map((path) => path.trim().replaceAll('\\', '/').replace(/^\.\//, ''))
    .filter(Boolean)
  const unique = [...new Set(candidates)]
  return {
    valid: unique.filter(isSafeRelativePath),
    invalid: unique.filter((path) => !isSafeRelativePath(path)),
  }
}

export function buildGitCommandPlan(input: GitCommandInput): GitCommandPlan {
  const taskCode = input.taskCode.trim().toUpperCase()
  const branchName = `${input.branchType}/${taskCode.toLowerCase()}-${slugifyBranch(input.taskTitle)}`
  const commitMessage = `${input.commitType}: ${safeCommitSummary(input.commitSummary)} #${taskCode}`
  const paths = parseStagePaths(input.stagePaths)
  const commands: GitCommand[] = [
    {
      id: 'base',
      label: 'Switch to the protected base branch',
      command: 'git switch main',
    },
    {
      id: 'pull',
      label: 'Fast-forward the base branch',
      command: 'git pull --ff-only origin main',
    },
    {
      id: 'branch',
      label: 'Create a task branch',
      command: `git switch -c ${branchName}`,
    },
    {
      id: 'status',
      label: 'Inspect changed files',
      command: 'git status --short',
    },
  ]

  if (paths.valid.length > 0) {
    commands.push({
      id: 'stage',
      label: 'Stage explicit paths only',
      command: `git add -- ${paths.valid.map((path) => `"${path}"`).join(' ')}`,
    })
  }

  commands.push(
    {
      id: 'review',
      label: 'Review exactly what will be committed',
      command: 'git diff --cached',
    },
    {
      id: 'commit',
      label: 'Commit with the Task Code',
      command: `git commit -m "${commitMessage}"`,
    },
    {
      id: 'push',
      label: 'Push the task branch',
      command: `git push -u origin ${branchName}`,
    },
  )

  return {
    branchName,
    commitMessage,
    commands,
    invalidStagePaths: paths.invalid,
    needsStagePaths: paths.valid.length === 0,
  }
}
