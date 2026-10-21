import { expect, test } from '@playwright/test'
import { writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('i18nextLng', 'en'))
  await page.goto('/')
})

test('answers a question with a cited source from the seeded documents', async ({ page }) => {
  await expect(page.getByText('employee-handbook', { exact: true })).toBeVisible()

  await page.getByLabel('Ask a question about your documents').fill('How many vacation days do I get per year?')
  await page.getByRole('button', { name: 'Ask' }).click()

  await expect(page.getByText('According to the documents')).toBeVisible()
  await expect(page.getByText(/\d+ tokens/)).toBeVisible()

  await page.getByText(/^Sources \(\d+\)/).click()
  await expect(page.getByText('25 paid vacation days').first()).toBeVisible()
})

test('keeps workspaces isolated', async ({ page }) => {
  await expect(page.getByText('refund-policy', { exact: true })).toBeVisible()

  await page.getByLabel('Workspace').click()
  await page.getByRole('option', { name: 'Globex' }).click()

  await expect(page.getByText('security-policy', { exact: true })).toBeVisible()
  await expect(page.getByText('refund-policy', { exact: true })).toHaveCount(0)
})

test('uploads a document', async ({ page }) => {
  const name = `canteen-${Date.now()}`
  const file = join(tmpdir(), `${name}.txt`)
  writeFileSync(file, 'The canteen serves lunch from noon until two in the afternoon.')

  await page.setInputFiles('[data-testid=file-input]', file)

  await expect(page.getByText(name, { exact: true })).toBeVisible()
})

test('switches the interface to Ukrainian', async ({ page }) => {
  await page.getByLabel('Language').click()
  await page.getByRole('option', { name: 'UK' }).click()

  await expect(page.getByRole('button', { name: 'Завантажити документ' })).toBeVisible()
  await expect(page.getByPlaceholder('Поставте запитання до ваших документів')).toBeVisible()
})
