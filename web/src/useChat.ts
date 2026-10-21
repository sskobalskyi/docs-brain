import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, askQuestion } from './api'
import { applyEvent, failTurn, startTurn, type Turn } from './chatState'

/** Chat history for one workspace. Starting a new question while one streams cancels the old one. */
export function useChat(token: string | undefined, workspaceKey: string) {
  const [turns, setTurns] = useState<Turn[]>([])
  const abort = useRef<AbortController | null>(null)
  const nextId = useRef(1)

  // A different workspace has a different document set, so start a fresh conversation.
  useEffect(() => {
    abort.current?.abort()
    setTurns([])
  }, [workspaceKey])

  const update = (id: number, change: (turn: Turn) => Turn) =>
    setTurns((all) => all.map((t) => (t.id === id ? change(t) : t)))

  const ask = useCallback(
    async (question: string) => {
      if (!token) return
      abort.current?.abort()
      const controller = new AbortController()
      abort.current = controller

      const id = nextId.current++
      setTurns((all) => [...all, startTurn(id, question)])

      try {
        await askQuestion(token, question, (event) => update(id, (t) => applyEvent(t, event)), controller.signal)
      } catch (error) {
        if (controller.signal.aborted) return update(id, (t) => ({ ...t, status: 'done' }))
        const status = error instanceof ApiError ? error.status : 0
        update(id, (t) => failTurn(t, status === 429 ? 'rate_limited' : status === 401 ? 'unauthorized' : 'failed'))
      }
    },
    [token],
  )

  const stop = useCallback(() => abort.current?.abort(), [])
  const streaming = turns.some((t) => t.status === 'streaming')

  return { turns, ask, stop, streaming }
}
