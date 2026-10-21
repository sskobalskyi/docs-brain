import { describe, expect, it } from 'vitest'
import { applyEvent, failTurn, splitCitations, startTurn, type Source } from './chatState'

const source = (number: number): Source => ({ number, documentId: 'd', title: 't', chunkIndex: 0, text: 'x' })

describe('applyEvent', () => {
  it('accumulates sources, tokens and the final mode', () => {
    let turn = startTurn(1, 'q')
    turn = applyEvent(turn, { type: 'sources', sources: [source(1)] })
    turn = applyEvent(turn, { type: 'token', text: 'Hello ' })
    turn = applyEvent(turn, { type: 'token', text: 'world' })
    turn = applyEvent(turn, { type: 'done', mode: 'answer', tokens: 42 })

    expect(turn).toMatchObject({ answer: 'Hello world', status: 'done', mode: 'answer', tokens: 42 })
    expect(turn.sources).toHaveLength(1)
  })

  it('keeps the sources when the model falls back', () => {
    let turn = applyEvent(startTurn(1, 'q'), { type: 'sources', sources: [source(1)] })
    turn = applyEvent(turn, { type: 'done', mode: 'retrieval_only', reason: 'budget_exceeded' })

    expect(turn).toMatchObject({ status: 'done', mode: 'retrieval_only', reason: 'budget_exceeded', answer: '' })
    expect(turn.sources).toHaveLength(1)
  })

  it('marks a failed request', () => {
    expect(failTurn(startTurn(1, 'q'), 'rate_limited')).toMatchObject({ status: 'error', error: 'rate_limited' })
  })
})

describe('splitCitations', () => {
  it('turns known [n] markers into citation parts', () => {
    expect(splitCitations('Refunds take 14 days [1]. Shipping is fast [2].', [source(1), source(2)])).toEqual([
      { kind: 'text', text: 'Refunds take 14 days ' },
      { kind: 'citation', number: 1 },
      { kind: 'text', text: '. Shipping is fast ' },
      { kind: 'citation', number: 2 },
      { kind: 'text', text: '.' },
    ])
  })

  it('leaves unknown markers as plain text', () => {
    expect(splitCitations('See [7].', [source(1)])).toEqual([{ kind: 'text', text: 'See [7].' }])
  })
})
