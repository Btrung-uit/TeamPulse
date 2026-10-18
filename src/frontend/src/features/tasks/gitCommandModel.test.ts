import { describe, expect, it } from 'vitest'

import {
  buildGitCommandPlan,
  defaultCommitSummary,
  parseStagePaths,
  slugifyBranch,
} from '@/features/tasks/gitCommandModel'

describe('Git command model', () => {
  it('creates a safe task branch and conventional commit carrying the task code', () => {
    const plan = buildGitCommandPlan({
      taskCode: 'TP-033',
      taskTitle: 'Tạo Git Command Generator',
      branchType: 'feature',
      commitType: 'feat',
      commitSummary: 'add command generator',
      stagePaths: 'src/frontend/src/App.tsx\ntests/App.test.tsx',
    })

    expect(plan.branchName).toBe('feature/tp-033-tao-git-command-generator')
    expect(plan.commitMessage).toBe('feat: add command generator #TP-033')
    expect(plan.commands.find((item) => item.id === 'stage')?.command).toBe(
      'git add -- "src/frontend/src/App.tsx" "tests/App.test.tsx"',
    )
    expect(plan.commands.at(-1)?.command).toBe(
      'git push -u origin feature/tp-033-tao-git-command-generator',
    )
  })

  it('never falls back to git add dot or pushes main when paths are missing', () => {
    const plan = buildGitCommandPlan({
      taskCode: 'TP-033',
      taskTitle: 'Generator',
      branchType: 'fix',
      commitType: 'fix',
      commitSummary: 'fix generator',
      stagePaths: '',
    })

    expect(plan.needsStagePaths).toBe(true)
    expect(plan.commands.some((item) => item.command === 'git add .')).toBe(
      false,
    )
    expect(
      plan.commands.some((item) => item.command === 'git push origin main'),
    ).toBe(false)
    expect(plan.commands.find((item) => item.id === 'stage')).toBeUndefined()
  })

  it('rejects absolute, traversal, wildcard and shell-like stage paths', () => {
    const paths = parseStagePaths(
      [
        '.',
        '../secret.txt',
        'C:\\secret.txt',
        'src/*.ts',
        'src/app.ts;echo bad',
        'src/app.ts',
      ].join('\n'),
    )

    expect(paths.valid).toEqual(['src/app.ts'])
    expect(paths.invalid).toHaveLength(5)
  })

  it('normalizes Vietnamese titles and provides an editable default summary', () => {
    expect(slugifyBranch('Đăng ký & Đăng nhập')).toBe('dang-ky-dang-nhap')
    expect(defaultCommitSummary('Build Task Detail')).toBe(
      'complete build task detail',
    )
  })
})
