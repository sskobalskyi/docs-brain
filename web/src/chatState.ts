export interface Source {
  number: number
  documentId: string
  title: string
  chunkIndex: number
  text: string
  shared?: boolean
}

export type ChatMode = 'answer' | 'retrieval_only' | 'no_results'

export type ChatEvent =
  | { type: 'sources'; sources: Source[] }
  | { type: 'token'; text: string }
  | { type: 'done'; mode: ChatMode; reason?: string; tokens?: number }

export interface Turn {
  id: number
  question: string
  answer: string
  sources: Source[]
  status: 'streaming' | 'done' | 'error'
  mode?: ChatMode
  reason?: string
  tokens?: number
  /** Set when the request itself failed (not when the model fell back). */
  error?: 'rate_limited' | 'unauthorized' | 'failed'
}

export function startTurn(id: number, question: string): Turn {
  return { id, question, answer: '', sources: [], status: 'streaming' }
}

export function applyEvent(turn: Turn, event: ChatEvent): Turn {
  switch (event.type) {
    case 'sources':
      return { ...turn, sources: event.sources }
    case 'token':
      return { ...turn, answer: turn.answer + event.text }
    case 'done':
      return { ...turn, status: 'done', mode: event.mode, reason: event.reason, tokens: event.tokens }
  }
}

export function failTurn(turn: Turn, error: NonNullable<Turn['error']>): Turn {
  return { ...turn, status: 'error', error }
}

export type AnswerPart = { kind: 'text'; text: string } | { kind: 'citation'; number: number }

/** Splits an answer into text and `[n]` citations, keeping only citations that point to a known source. */
export function splitCitations(answer: string, sources: Source[]): AnswerPart[] {
  const known = new Set(sources.map((s) => s.number))
  const parts: AnswerPart[] = []
  let last = 0

  for (const match of answer.matchAll(/\[(\d+)\]/g)) {
    const number = Number(match[1])
    if (!known.has(number)) continue
    if (match.index > last) parts.push({ kind: 'text', text: answer.slice(last, match.index) })
    parts.push({ kind: 'citation', number })
    last = match.index + match[0].length
  }

  if (last < answer.length) parts.push({ kind: 'text', text: answer.slice(last) })
  return parts
}
