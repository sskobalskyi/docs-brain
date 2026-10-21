import { createSseParser } from './sse'
import type { ChatEvent } from './chatState'

// Demo tenants seeded by the API. Tokens come from the development-only /dev/token endpoint.
export const TENANTS = [
  { id: '11111111-1111-1111-1111-111111111111', name: 'Acme' },
  { id: '22222222-2222-2222-2222-222222222222', name: 'Globex' },
] as const

export interface DocumentItem {
  id: string
  title: string
  fileName: string | null
  createdAt: string
}

export class ApiError extends Error {
  readonly status: number
  readonly detail?: string

  constructor(status: number, detail?: string) {
    super(`API request failed with ${status}`)
    this.status = status
    this.detail = detail
  }
}

async function check(response: Response): Promise<Response> {
  if (response.ok) return response
  let detail: string | undefined
  try {
    detail = ((await response.json()) as { title?: string }).title
  } catch {
    // The body is not a problem document.
  }
  throw new ApiError(response.status, detail)
}

export async function getToken(tenantId: string): Promise<string> {
  const response = await check(
    await fetch('/dev/token', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ tenantId }),
    }),
  )
  return response.text()
}

const auth = (token: string) => ({ authorization: `Bearer ${token}` })

export async function listDocuments(token: string): Promise<DocumentItem[]> {
  const response = await check(await fetch('/api/documents', { headers: auth(token) }))
  return response.json()
}

export async function uploadDocument(token: string, file: File): Promise<DocumentItem> {
  const body = new FormData()
  body.append('file', file)
  const response = await check(await fetch('/api/documents/upload', { method: 'POST', headers: auth(token), body }))
  return response.json()
}

/** Streams a chat answer, calling `onEvent` for every server-sent event. */
export async function askQuestion(
  token: string,
  question: string,
  onEvent: (event: ChatEvent) => void,
  signal?: AbortSignal,
): Promise<void> {
  const response = await check(
    await fetch('/api/chat', {
      method: 'POST',
      headers: { ...auth(token), 'content-type': 'application/json' },
      body: JSON.stringify({ question }),
      signal,
    }),
  )

  const reader = response.body!.pipeThrough(new TextDecoderStream()).getReader()
  const push = createSseParser((data) => onEvent(JSON.parse(data) as ChatEvent))
  for (;;) {
    const { done, value } = await reader.read()
    if (done) return
    push(value)
  }
}
