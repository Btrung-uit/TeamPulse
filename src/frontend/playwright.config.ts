import { defineConfig, devices } from '@playwright/test'

const apiUrl = 'http://127.0.0.1:5249'
const webUrl = 'http://127.0.0.1:4173'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  timeout: 90_000,
  expect: { timeout: 10_000 },
  outputDir: 'test-results',
  reporter: process.env.CI
    ? [['line'], ['html', { open: 'never' }]]
    : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: webUrl,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: [
    {
      command:
        'dotnet run --project ../backend/TeamPulse.Api/TeamPulse.Api.csproj --no-launch-profile --urls http://127.0.0.1:5249',
      url: `${apiUrl}/health/ready`,
      timeout: 120_000,
      reuseExistingServer: false,
      stdout: 'pipe',
      stderr: 'pipe',
      env: {
        ASPNETCORE_ENVIRONMENT: 'E2E',
        Database__Provider: 'InMemory',
        Database__Name: `TeamPulseE2E-${process.pid}`,
        Authentication__Jwt__SigningKey:
          'TeamPulse-E2E-only-signing-key-32-bytes-minimum',
      },
    },
    {
      command: 'npm run dev -- --host 127.0.0.1 --port 4173',
      url: webUrl,
      timeout: 120_000,
      reuseExistingServer: false,
      stdout: 'pipe',
      stderr: 'pipe',
    },
  ],
})
