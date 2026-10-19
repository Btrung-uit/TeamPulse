import {
  expect,
  test,
  type APIRequestContext,
  type BrowserContext,
  type Locator,
  type Page,
} from '@playwright/test'
import AxeBuilder from '@axe-core/playwright'

const password = 'TeamPulse123'

interface AuthSession {
  accessToken: string
}

interface Workspace {
  id: string
}

async function activate(locator: Locator) {
  await locator.focus()
  await expect(locator).toBeFocused()
  await locator.press('Enter')
}

async function expectAccessible(page: Page, context: string) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze()

  expect(
    results.violations,
    `${context}: ${results.violations
      .map((violation) => `${violation.id} (${violation.nodes.length})`)
      .join(', ')}`,
  ).toEqual([])
}

async function register(
  request: APIRequestContext,
  email: string,
  displayName: string,
) {
  const response = await request.post('/api/auth/register', {
    data: { email, displayName, password },
  })
  expect(response, `register ${email}`).toBeOK()
  return (await response.json()) as AuthSession
}

async function seedTeam(request: APIRequestContext) {
  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
  const leaderEmail = `leader-${suffix}@example.com`
  const memberEmail = 'member@example.com'

  await register(request, memberEmail, 'Demo Member')
  const leader = await register(request, leaderEmail, 'Demo Leader')
  const authorization = { Authorization: `Bearer ${leader.accessToken}` }
  const workspaceResponse = await request.post('/api/workspaces/', {
    headers: authorization,
    data: { name: 'E2E Demo Workspace' },
  })
  expect(workspaceResponse, 'create workspace').toBeOK()
  const workspace = (await workspaceResponse.json()) as Workspace

  const memberResponse = await request.post(
    `/api/workspaces/${workspace.id}/members`,
    {
      headers: authorization,
      data: { email: memberEmail, role: 'Member' },
    },
  )
  expect(memberResponse, 'add workspace member').toBeOK()

  return { leaderEmail, memberEmail }
}

async function signIn(page: Page, email: string) {
  await page.goto('/login')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(password)
  await activate(page.getByRole('button', { name: 'Sign in' }))
  await expect(
    page.getByRole('heading', { name: 'Projects', exact: true }),
  ).toBeVisible()
}

test('leader import → member evidence → leader review → presentation', async ({
  browser,
  page,
  request,
}, testInfo) => {
  const { leaderEmail, memberEmail } = await seedTeam(request)
  const workbookPath = testInfo.outputPath('core-demo-tasks.xlsx')
  let leaderObserverContext: BrowserContext | undefined
  let leaderObserverPage: Page | undefined

  await test.step('Leader signs in and creates a project', async () => {
    await signIn(page, leaderEmail)
    await activate(page.getByRole('button', { name: 'Create first project' }))
    await page.getByLabel('Project name').fill('TeamPulse Core Demo')
    await page
      .getByLabel('Description')
      .fill('A complete task, evidence, review and reporting walkthrough.')
    await activate(page.getByRole('button', { name: 'Create project' }))
    await expect(page).toHaveURL(/\/workspaces\/[^/]+\/projects\/[^/]+$/)
    await expect(
      page.getByRole('heading', { name: 'Project dashboard' }),
    ).toBeVisible()
    await expectAccessible(page, 'empty project dashboard')
    await activate(page.getByRole('button', { name: 'Switch to dark theme' }))
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
    await expectAccessible(page, 'dark theme dashboard')
    await activate(page.getByRole('button', { name: 'Switch to light theme' }))
  })

  await test.step('Leader uploads, previews and confirms the Excel import', async () => {
    await activate(page.getByRole('link', { name: 'Task board', exact: true }))
    const downloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: 'Download template' }).click()
    await (await downloadPromise).saveAs(workbookPath)
    await activate(page.getByRole('link', { name: /Import Excel/ }))
    await page.getByLabel('Excel workbook').setInputFiles(workbookPath)
    await activate(page.getByRole('button', { name: 'Upload' }))
    await expect(
      page.getByRole('heading', { name: /Select sheet and map columns/ }),
    ).toBeVisible()
    await page.getByLabel('Sheet').selectOption({
      label: 'Example - do not import (2 rows)',
    })
    await activate(page.getByRole('button', { name: 'Validate preview' }))
    await expect(page.getByText('2 valid · 0 invalid · 2 total')).toBeVisible()
    await expectAccessible(page, 'Excel validation preview')
    await activate(page.getByRole('button', { name: 'Confirm import' }))
    await activate(page.getByRole('button', { name: 'Import 2 tasks' }))
    await expect(page.getByText('2 tasks imported.')).toBeVisible()
  })

  await test.step('Leader keeps a second session open for live updates', async () => {
    leaderObserverContext = await browser.newContext()
    leaderObserverPage = await leaderObserverContext.newPage()
    await signIn(leaderObserverPage, leaderEmail)
    await activate(
      leaderObserverPage.getByRole('link', { name: 'TeamPulse Core Demo' }),
    )
    await activate(
      leaderObserverPage.getByRole('link', {
        name: 'Task board',
        exact: true,
      }),
    )
    await expect(
      leaderObserverPage.getByRole('button', {
        name: 'Open TP-001: Build login page',
      }),
    ).toBeVisible()
  })

  await test.step('Member links evidence and submits the task for review', async () => {
    await activate(page.getByRole('button', { name: /Sign out/ }))
    await signIn(page, memberEmail)
    await activate(page.getByRole('link', { name: 'TeamPulse Core Demo' }))
    await activate(page.getByRole('link', { name: 'Task board', exact: true }))
    await activate(
      page.getByRole('button', {
        name: 'Open TP-001: Build login page',
      }),
    )
    await expect(
      page.getByRole('heading', { name: 'Build login page' }),
    ).toBeVisible()
    await expectAccessible(page, 'member task details')
    await activate(page.getByRole('button', { name: 'In progress' }))
    await expect(
      page.locator('.task-detail-panel .ui-status-badge'),
    ).toHaveText('In progress')
    await page
      .getByLabel('Evidence URL')
      .fill('https://github.com/example/teampulse/pull/501')
    await page.getByLabel('Evidence note').fill('Core demo flow verified.')
    await activate(page.getByRole('button', { name: 'Link evidence' }))
    await expect(
      page.getByRole('link', { name: 'Open evidence' }),
    ).toBeVisible()
    await activate(page.getByRole('button', { name: 'In review' }))
    await expect(
      page.locator('.task-detail-panel .ui-status-badge'),
    ).toHaveText('In review')
    await activate(page.getByRole('button', { name: 'Close task detail' }))

    const observedTask = leaderObserverPage!
      .getByRole('button', { name: 'Open TP-001: Build login page' })
      .locator('..')
    await expect(observedTask.getByText('In review')).toBeVisible({
      timeout: 12_000,
    })
    await leaderObserverContext!.close()
  })

  await test.step('Leader accepts the task', async () => {
    await activate(page.getByRole('button', { name: /Sign out/ }))
    await signIn(page, leaderEmail)
    await activate(page.getByRole('link', { name: 'TeamPulse Core Demo' }))
    await activate(page.getByRole('link', { name: 'Task board', exact: true }))
    await activate(
      page.getByRole('button', {
        name: 'Open TP-001: Build login page',
      }),
    )
    await page.getByLabel('Review comment').fill('Accepted for the demo.')
    await activate(page.getByRole('button', { name: 'Accept task' }))
    await expect(page.getByText('Accepted by Demo Leader')).toBeVisible()
    await activate(page.getByRole('button', { name: 'Close task detail' }))
  })

  await test.step('Dashboard and presentation show the accepted outcome', async () => {
    await activate(page.getByRole('link', { name: 'Dashboard', exact: true }))
    await expect(page.getByLabel('50 percent complete')).toBeVisible()
    await expectAccessible(page, 'project dashboard')
    await activate(page.getByRole('link', { name: 'Present project' }))
    await expect(
      page.getByRole('heading', { name: 'TeamPulse Core Demo' }),
    ).toBeVisible()
    await expect(page.getByText('1 of 2 tasks accepted')).toBeVisible()
    await expect(
      page.getByText('1 accepted evidence · 1 reviews'),
    ).toBeVisible()
    await expectAccessible(page, 'presentation mode')

    const presentationHeading = page.getByRole('heading', {
      name: 'TeamPulse Core Demo',
    })
    await expect(presentationHeading).toHaveCSS('color', 'rgb(247, 248, 251)')

    const firstKpi = page.locator('.presentation-kpis article').first()
    const numberBox = await firstKpi.locator('strong').boundingBox()
    const labelBox = await firstKpi.locator('span').boundingBox()
    expect(numberBox).not.toBeNull()
    expect(labelBox).not.toBeNull()
    expect(numberBox!.y + numberBox!.height).toBeLessThanOrEqual(labelBox!.y)

    const toolbar = page.locator('.presentation-toolbar')
    const toolbarTop = (await toolbar.boundingBox())?.y
    await toolbar.getByRole('link', { name: 'Resources', exact: true }).click()
    await expect(page.locator('#resources')).toBeInViewport()
    const toolbarTopAfterScroll = (await toolbar.boundingBox())?.y
    expect(
      Math.abs((toolbarTopAfterScroll ?? 0) - (toolbarTop ?? 0)),
    ).toBeLessThanOrEqual(1)
  })
})
