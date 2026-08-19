# Implementation architecture

## 1. Components

```text
CLI parser
   │
   ▼
Search planner ──► provider selector / capability checks
   │
   ▼
Concurrent executor
   ├── Libex adapter
   ├── AudioSilo adapter
   ├── Generic ABS adapter ──► arbitrary configured ABS providers
   └── future adapters
   │
   ▼
Normalizer
   │
   ▼
Local ranker
   │
   ▼
Work + edition clustering
   │
   ▼
Renderer (table / JSON / JSONL)
```

## 2. Search planner

The planner decides which calls are useful without sending a combinatorial number of requests.

Example Libex plan for `proj hail --author weir`:

1. quick-search with keywords `proj hail`;
2. structured `/search` with title/query and author only if candidate budget is not already satisfied or exact mode is off;
3. merge provider-local duplicates;
4. normalize.

Example generic ABS plan:

1. one `GET /search?query=proj%20hail&author=weir` request;
2. local rank only.

## 3. Concurrency

Use structured concurrency with cancellation. Each provider task receives:

- per-provider timeout;
- shared global deadline;
- cancellation token;
- HTTP client/pool;
- cache handle;
- request ID for diagnostics.

The executor caps simultaneous provider calls (default 8). A provider adapter that performs multiple internal requests SHOULD also enforce its own small concurrency bound.

## 4. Retries

Only idempotent GETs are retried. Suggested policy:

- network reset/temporary DNS: at most 1 retry;
- HTTP 429: honor `Retry-After` if within remaining deadline;
- HTTP 502/503/504: at most 1 retry with jitter;
- 400/401/403/404: no generic retry, except an adapter may treat documented 404-as-empty as success.

## 5. Caching

Two optional layers:

1. normalized search cache keyed by provider ID + normalized request + adapter version;
2. HTTP response cache for GETs where upstream semantics permit.

Default normalized search TTL: 15 minutes. `--fresh` bypasses CLI caches but does not promise to bypass an upstream provider's own cache unless that provider documents a cache-control parameter and the adapter maps it.

Do not cache authentication failures. Negative/empty-result caching SHOULD use a shorter TTL.

## 6. HTTP behavior

- Follow same-host redirects by default.
- Cross-host redirects on authenticated requests require opt-in.
- User-Agent SHOULD identify `bookmeta/<version>`.
- Compression SHOULD be enabled.
- Response body size limits SHOULD protect against misbehaving providers (suggested 10 MiB search limit; configurable for native detail endpoints).

## 7. Parsing

Provider responses are untrusted. Parsers SHOULD:

- enforce expected JSON types;
- tolerate absent optional fields;
- reject pathological nesting/oversized arrays;
- never render raw HTML control sequences directly;
- normalize durations carefully because provider ecosystems may use different units outside the ABS contract.

## 8. Observability

`--explain` is user-facing diagnostics. A separate `--debug` MAY log low-level requests with secret redaction.

Useful debug fields:

- provider ID;
- method + redacted URL;
- HTTP status;
- elapsed time;
- candidate counts before/after normalization;
- cache hit/miss;
- retry count;
- ranking summary.

## 9. Suggested module layout

Language-neutral package boundaries:

```text
cmd/
config/
model/
provider/
  abs/
  libex/
  audiosilo/
search/
  planner/
  normalize/
  rank/
  cluster/
cache/
render/
fixtures/
```

## 10. Implementation phases

### Phase 1 — functional MVP

- config loader/validator;
- generic ABS adapter;
- Libex adapter;
- normalized models;
- concurrent `search`;
- provider selection;
- human + JSON output;
- basic local ranking;
- fixture tests.

### Phase 2 — incomplete search quality

- Libex quick-search planning;
- token-prefix/fuzzy score;
- work/edition clustering;
- `--explain`, `--no-dedupe`, `--editions`;
- AudioSilo native adapter.

### Phase 3 — operational hardening

- cache;
- retries/rate-limit handling;
- provider health/tests;
- shell completion;
- strict capability diagnostics;
- cross-platform secret handling.

### Phase 4 — richer metadata navigation

Optional additions backed by providers that support them:

- author search/details;
- series search/details;
- chapters;
- new releases / coming soon;
- categories/genres;
- compare/resolve view for field-by-field provenance.
