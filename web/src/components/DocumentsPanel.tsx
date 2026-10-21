import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError, listDocuments, uploadDocument } from '../api'
import { Alert, AlertDescription } from './ui/alert'
import { Button } from './ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from './ui/card'
import { ScrollArea } from './ui/scroll-area'

export function DocumentsPanel({ token, tenantId }: { token: string | undefined; tenantId: string }) {
  const { t } = useTranslation()
  const client = useQueryClient()
  const input = useRef<HTMLInputElement>(null)
  const key = ['documents', tenantId]

  const documents = useQuery({ queryKey: key, queryFn: () => listDocuments(token!), enabled: token !== undefined })
  const upload = useMutation({
    mutationFn: (file: File) => uploadDocument(token!, file),
    onSuccess: () => client.invalidateQueries({ queryKey: key }),
  })

  const reason = upload.error instanceof ApiError ? (upload.error.detail ?? String(upload.error.status)) : upload.error?.message

  return (
    <Card className="min-h-0" aria-labelledby="documents-title">
      <CardHeader>
        <CardTitle id="documents-title">{t('documents.title')}</CardTitle>
        <CardDescription>{t('documents.hint')}</CardDescription>
      </CardHeader>
      <CardContent className="flex min-h-0 flex-col gap-3">
        <input
          ref={input}
          type="file"
          accept=".pdf,.docx,.txt,.md"
          hidden
          data-testid="file-input"
          onChange={(e) => {
            const file = e.target.files?.[0]
            if (file) upload.mutate(file)
            e.target.value = ''
          }}
        />
        <Button type="button" disabled={token === undefined || upload.isPending} onClick={() => input.current?.click()}>
          {upload.isPending ? t('documents.uploading') : t('documents.upload')}
        </Button>

        {upload.isError && (
          <Alert variant="destructive">
            <AlertDescription>{t('error.upload', { reason })}</AlertDescription>
          </Alert>
        )}
        {documents.isError && (
          <Alert variant="destructive">
            <AlertDescription>{t('documents.loadFailed')}</AlertDescription>
          </Alert>
        )}
        {documents.data?.length === 0 && <p className="text-sm text-muted-foreground">{t('documents.empty')}</p>}

        <ScrollArea className="min-h-0 flex-1">
          <ul className="flex flex-col gap-2">
            {documents.data?.map((doc) => (
              <li key={doc.id} className="flex flex-col rounded-lg border p-2">
                <span className="font-medium break-words">{doc.title}</span>
                <span className="text-xs text-muted-foreground break-words">{doc.fileName}</span>
              </li>
            ))}
          </ul>
        </ScrollArea>
      </CardContent>
    </Card>
  )
}
