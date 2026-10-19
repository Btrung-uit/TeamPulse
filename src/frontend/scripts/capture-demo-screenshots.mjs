import fs from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from '@playwright/test'

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url))
const outputDirectory = process.env.TEAMPULSE_SCREENSHOT_DIR
  ? path.resolve(process.env.TEAMPULSE_SCREENSHOT_DIR)
  : path.resolve(scriptDirectory, '..', '..', '..', 'docs', 'screenshots')
const baseUrl = process.env.TEAMPULSE_WEB_URL ?? 'http://127.0.0.1:4173'
const password = process.env.TEAMPULSE_DEMO_PASSWORD ?? 'TeamPulseDemo2026!'

await fs.mkdir(outputDirectory, { recursive: true })
const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })

try {
  await page.goto(`${baseUrl}/login`)
  await page.getByLabel('Email').fill('bao.trung@teampulse.demo')
  await page.getByLabel('Password').fill(password)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await page.getByRole('heading', { name: 'Projects', exact: true }).waitFor()
  await page
    .getByRole('link', { name: /CampusEvent/ })
    .first()
    .click()
  await page.getByRole('heading', { name: 'Project dashboard' }).waitFor()
  await page.screenshot({
    path: path.join(outputDirectory, 'campusevent-dashboard.png'),
    fullPage: true,
  })

  await page.getByRole('link', { name: 'Task board', exact: true }).click()
  await page.getByRole('heading', { name: 'Task board', exact: true }).waitFor()
  const layoutViolations = await page
    .locator('.task-column__cards')
    .evaluateAll((columns) =>
      columns.flatMap((column, columnIndex) => {
        const cards = [...column.querySelectorAll('.ui-task-card')]
        return cards.flatMap((card, cardIndex) => {
          const cardBox = card.getBoundingClientRect()
          const footerBox = card
            .querySelector('.ui-task-card__footer')
            ?.getBoundingClientRect()
          const nextBox = cards[cardIndex + 1]?.getBoundingClientRect()
          const issues = []
          if (footerBox && footerBox.bottom > cardBox.bottom + 1) {
            issues.push(
              `column ${columnIndex + 1}, card ${cardIndex + 1}: footer overflow`,
            )
          }
          if (nextBox && nextBox.top < cardBox.bottom) {
            issues.push(
              `column ${columnIndex + 1}, card ${cardIndex + 1}: overlaps next card`,
            )
          }
          return issues
        })
      }),
    )
  if (layoutViolations.length > 0) {
    throw new Error(`Invalid task card layout: ${layoutViolations.join('; ')}`)
  }
  await page.screenshot({
    path: path.join(outputDirectory, 'campusevent-task-board.png'),
    fullPage: true,
  })

  await page.getByRole('link', { name: 'Presentation mode' }).click()
  await page.getByRole('heading', { name: 'CampusEvent' }).waitFor()
  await page.screenshot({
    path: path.join(outputDirectory, 'campusevent-presentation.png'),
    fullPage: true,
  })
} finally {
  await browser.close()
}

console.log(`Saved TeamPulse screenshots to ${outputDirectory}`)
