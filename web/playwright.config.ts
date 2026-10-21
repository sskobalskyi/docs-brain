import { defineConfig } from '@playwright/test'

// Runs against the full Docker stack: COMPOSE_PROFILES= AI_PROVIDER=mock SEARCH_MIN_SIMILARITY=0.2 docker compose up -d --build --wait
export default defineConfig({
  testDir: 'e2e',
  use: { baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8090', trace: 'retain-on-failure' },
  reporter: [['list']],
})
