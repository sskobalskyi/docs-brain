import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { splitCitations, type Turn } from '../chatState'
import type { useChat } from '../useChat'
import { Alert, AlertDescription } from './ui/alert'
import { Badge } from './ui/badge'
import { Button } from './ui/button'
import { Card, CardContent } from './ui/card'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from './ui/collapsible'
import { Input } from './ui/input'
import { ScrollArea } from './ui/scroll-area'

type Chat = ReturnType<typeof useChat>

export function ChatPanel({ chat, ready }: { chat: Chat; ready: boolean }) {
  const { t } = useTranslation()
  const [question, setQuestion] = useState('')
  const end = useRef<HTMLDivElement>(null)

  useEffect(() => {
    end.current?.scrollIntoView?.({ block: 'end' })
  }, [chat.turns])

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const text = question.trim()
    if (!text) return
    setQuestion('')
    void chat.ask(text)
  }

  return (
    <Card className="min-h-[420px] min-w-0" aria-label={t('chat.title')}>
      <CardContent className="flex min-h-0 flex-1 flex-col gap-3">
        <ScrollArea className="min-h-0 flex-1" aria-live="polite">
          <div className="flex flex-col gap-6 pr-3">
            {chat.turns.length === 0 && (
              <p className="py-16 text-center text-sm text-muted-foreground">{t('chat.empty')}</p>
            )}
            {chat.turns.map((turn) => (
              <TurnView key={turn.id} turn={turn} />
            ))}
            <div ref={end} />
          </div>
        </ScrollArea>

        <form className="flex gap-2" onSubmit={submit}>
          <Input
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            placeholder={t('chat.placeholder')}
            aria-label={t('chat.placeholder')}
            disabled={!ready}
          />
          {chat.streaming ? (
            <Button type="button" variant="outline" onClick={chat.stop}>
              {t('chat.stop')}
            </Button>
          ) : (
            <Button type="submit" disabled={!ready || question.trim() === ''}>
              {t('chat.ask')}
            </Button>
          )}
        </form>
      </CardContent>
    </Card>
  )
}

function TurnView({ turn }: { turn: Turn }) {
  const { t } = useTranslation()
  const [highlight, setHighlight] = useState<number>()

  const notice =
    turn.mode === 'no_results' ? 'no_results' : turn.mode === 'retrieval_only' ? (turn.reason ?? 'retrieval_only') : undefined

  return (
    <article className="flex flex-col gap-2">
      <p className="font-semibold">{turn.question}</p>

      {turn.status === 'streaming' && turn.answer === '' && turn.sources.length === 0 && (
        <p className="text-sm text-muted-foreground">{t('chat.thinking')}</p>
      )}

      {turn.answer && (
        <p className="leading-relaxed whitespace-pre-wrap">
          {splitCitations(turn.answer, turn.sources).map((part, i) =>
            part.kind === 'text' ? (
              <span key={i}>{part.text}</span>
            ) : (
              <Button
                key={i}
                type="button"
                variant="secondary"
                size="xs"
                className="mx-0.5 h-5 px-1.5 align-super"
                onClick={() => setHighlight(part.number)}
              >
                {part.number}
              </Button>
            ),
          )}
        </p>
      )}

      {notice && (
        <Alert>
          <AlertDescription>{t(`notice.${notice}`)}</AlertDescription>
        </Alert>
      )}
      {turn.error && (
        <Alert variant="destructive">
          <AlertDescription>{t(`error.${turn.error}`)}</AlertDescription>
        </Alert>
      )}

      {turn.sources.length > 0 && (
        <Collapsible defaultOpen={turn.mode === 'retrieval_only'}>
          <CollapsibleTrigger className="text-sm font-medium underline-offset-4 hover:underline">
            {t('chat.sources')} ({turn.sources.length})
          </CollapsibleTrigger>
          <CollapsibleContent>
            <ol className="mt-2 flex flex-col gap-2">
              {turn.sources.map((source) => (
                <li
                  key={source.number}
                  className={cn('rounded-lg border p-3', highlight === source.number && 'border-primary bg-accent')}
                >
                  <div className="flex items-center gap-2">
                    <Badge variant="outline">[{source.number}]</Badge>
                    <strong className="break-words">{source.title}</strong>
                    <span className="text-xs text-muted-foreground">{t('chat.chunk', { n: source.chunkIndex + 1 })}</span>
                  </div>
                  <p className="mt-1 text-sm break-words whitespace-pre-wrap">{source.text}</p>
                </li>
              ))}
            </ol>
          </CollapsibleContent>
        </Collapsible>
      )}

      {turn.tokens !== undefined && <p className="text-xs text-muted-foreground">{t('tokens', { count: turn.tokens })}</p>}
    </article>
  )
}
