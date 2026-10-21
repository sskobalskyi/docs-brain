# 6. Web UI with React, shadcn/ui and a same-origin API

## Status
Accepted

## Decision
- React 19 + TypeScript + Vite, TanStack Query for server state, react-i18next with `en` (default) and `uk`.
- All UI is built from shadcn/ui components (Tailwind CSS v4, Base UI primitives) living in `web/src/components/ui`. Dark mode follows the OS through the `dark` class.
- Chat streaming uses `fetch` with a POST and a small incremental SSE parser (`sse.ts`), because `EventSource` cannot send an `Authorization` header. Chat state is a pure reducer (`chatState.ts`) covered by unit tests.
- The browser only talks to its own origin. Vite (development) and nginx (Docker) forward `/api` and `/dev` to the API, so the API needs no CORS configuration.
- The demo signs in through the development-only `/dev/token` endpoint and switches between the seeded Acme and Globex tenants.

## Consequences
- The UI works only against an API running in the Development environment until a real identity provider is added.
- shadcn components are copied into the repo, so they are ours to maintain.
