import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

const api = process.env.VITE_API_PROXY ?? 'http://localhost:5201'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@': path.resolve(import.meta.dirname, 'src') } },
  // The browser always talks to the same origin; Vite (dev) and nginx (Docker) forward these paths to the API.
  server: { proxy: { '/api': api, '/dev': api } },
  test: {
    include: ['src/**/*.test.ts'],
  },
})
