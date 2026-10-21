# 5. Streamed chat with citations and cost governance

## Status
Accepted

## Context
Answers must cite their sources, stream to the UI, and never make LLM cost unbounded. The assistant has to stay useful when the model is unavailable.

## Decision
- `POST /api/chat` returns `text/event-stream` (a POST, not `EventSource`, so the bearer token can be sent). Events: `sources` (numbered excerpts), `token` (answer fragments), `done` (`mode`, optional `reason`, `tokens`).
- The prompt contains numbered sources and instructs the model to cite them as `[1]`, `[2]`, to answer only from them, and to answer in the language of the question.
- `IChatClient` (Microsoft.Extensions.AI) is backed by Ollama (`llama3.2:3b`) or by a deterministic extractive mock used in tests and CI.
- Daily token budget per tenant (`UsageRecord`, one row per tenant and UTC day, `Chat:DailyTokenBudget`). Tokens are the provider's reported usage, or an estimate of characters / 4.
- Retrieval-only fallback: when the budget is exhausted or the model fails, the `sources` event is still sent and `done` carries `mode = retrieval_only` and a `reason` (`budget_exceeded`, `llm_unavailable`). When nothing relevant is found the model is not called (`mode = no_results`).
- Rate limit of `Chat:RequestsPerMinute` per tenant (fixed window) on the chat endpoint.
- Vector matches below `Search:MinSimilarity` are dropped, so unrelated questions do not return unrelated sources. The right value depends on the embedding model.

## Consequences
- The budget is checked before a request and charged after it, so one request can overshoot by its own size.
- Answer caching is not implemented yet.
- `Search:MinSimilarity` needs tuning when the embedding model changes.
