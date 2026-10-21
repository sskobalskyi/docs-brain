import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { getToken, TENANTS } from './api'
import { ChatPanel } from './components/ChatPanel'
import { DocumentsPanel } from './components/DocumentsPanel'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from './components/ui/select'
import { LOCALES } from './i18n'
import { useChat } from './useChat'

export default function App() {
  const { t, i18n } = useTranslation()
  const [tenantId, setTenantId] = useState<string>(TENANTS[0].id)

  const { data: token } = useQuery({ queryKey: ['token', tenantId], queryFn: () => getToken(tenantId), staleTime: 60 * 60_000 })
  const chat = useChat(token, tenantId)

  const tenantItems = TENANTS.map((tenant) => ({ value: tenant.id, label: tenant.name }))
  const localeItems = LOCALES.map((locale) => ({ value: locale, label: locale.toUpperCase() }))

  return (
    <div className="mx-auto flex min-h-screen max-w-5xl flex-col gap-4 p-4 md:h-screen">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold">{t('app.title')}</h1>
          <p className="text-sm text-muted-foreground">{t('app.tagline')}</p>
        </div>
        <div className="flex gap-2">
          <Select items={tenantItems} value={tenantId} onValueChange={(value) => value && setTenantId(value)}>
            <SelectTrigger aria-label={t('tenant')} className="w-32">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {tenantItems.map((item) => (
                <SelectItem key={item.value} value={item.value}>
                  {item.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select
            items={localeItems}
            value={i18n.resolvedLanguage}
            onValueChange={(value) => value && void i18n.changeLanguage(value)}
          >
            <SelectTrigger aria-label={t('language')} className="w-20">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {localeItems.map((item) => (
                <SelectItem key={item.value} value={item.value}>
                  {item.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </header>

      <main className="grid min-h-0 flex-1 gap-4 md:grid-cols-[280px_1fr]">
        <DocumentsPanel token={token} tenantId={tenantId} />
        <ChatPanel chat={chat} ready={token !== undefined} />
      </main>
    </div>
  )
}
