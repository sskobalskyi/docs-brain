# 7. Calls from other services: external tokens, tenant header and trusted context

## Status
Accepted

## Context
In the Greenhaus ecosystem a product API owns the rules (plans, quotas, what data a user may see) and calls Docs-Brain to answer questions grounded in the state of a user's plant pot. Docs-Brain has to stay generic and run standalone.

## Decision
- A second JWT scheme validates tokens of an external OpenID Connect authority (`Auth:Authority`, optional `Auth:Issuer` when tokens carry a different host name than the one the API reaches the authority at, `Auth:Audience`). The dev token and the external tokens are told apart by issuer and each is validated only by its own scheme. With no authority configured, external tokens are not accepted.
- Realm roles are read from `realm_access.roles` and exposed as `role` claims.
- A caller holding the service role (`Auth:ServiceRole`, default `docs-brain.assistant`) names the tenant in the `X-Tenant-Id` header. For every other caller the header is ignored, so a user token cannot switch tenant. A request that resolves to no tenant is forbidden (fail closed). The tenant claim of a token wins over the header.
- Unknown tenants need no setup: documents, usage and budgets are keyed by the tenant id.
- `POST /api/chat` accepts an optional `context` (plain facts, at most `Chat:MaxContextChars` = 4000 characters, otherwise 400). It goes into the prompt in a delimited `<context>` block, marked as data whose instructions must not be followed, and the closing tag is stripped from it. It is not a numbered source and is never cited.
- With a context, a question with no retrieved passages still calls the model. Without either, the answer is `no_results` as before.
- The daily token budget and the rate limit apply to service calls, keyed by the resolved tenant. Product quotas (for example questions per month by plan) belong to the calling service, which reads `done.mode`: only `answer` consumed model output.

## Consequences
- The service role is powerful: it can act for any tenant. The calling service must be the only holder of that client secret.
- The service token can reach every tenant-scoped endpoint, not only chat. Narrowing it to chat is a possible hardening.
- Prompt injection through the context is mitigated by delimiting and instruction, not eliminated; the caller keeps user-typed text inside a clearly marked section.
