import { describe, expect, it } from 'vitest'
import { createSseParser } from './sse'

function collect() {
  const events: string[] = []
  return { events, push: createSseParser((d) => events.push(d)) }
}

describe('createSseParser', () => {
  it('emits one event per blank-line separated block', () => {
    const { events, push } = collect()
    push('data: {"a":1}\n\ndata: {"b":2}\n\n')
    expect(events).toEqual(['{"a":1}', '{"b":2}'])
  })

  it('reassembles events split across chunks', () => {
    const { events, push } = collect()
    push('data: {"te')
    push('xt":"hi"}\n')
    expect(events).toEqual([])
    push('\n')
    expect(events).toEqual(['{"text":"hi"}'])
  })

  it('handles CRLF and ignores non-data lines', () => {
    const { events, push } = collect()
    push('event: token\r\ndata: x\r\n\r\n: comment\n\n')
    expect(events).toEqual(['x'])
  })
})
