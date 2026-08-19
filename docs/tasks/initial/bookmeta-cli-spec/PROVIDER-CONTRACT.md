# Provider adapter contract

## 1. Adapter model

Every configured provider instance uses an adapter. Multiple instances may use the same adapter with different URLs, regions, credentials, or provider-specific path parameters.

Normative conceptual interface:

```text
MetadataProvider
  id() -> string
  adapter_type() -> string
  capabilities() -> ProviderCapabilities
  search(SearchRequest, RequestContext) -> ProviderSearchResponse
  get(ProviderRecordRef, RequestContext) -> SearchResult   [optional]
  health(RequestContext) -> ProviderHealth                 [optional]
```

A provider adapter MUST NOT mutate global search state. It returns provider-native candidates normalized as far as reliably possible and leaves cross-provider ranking/clustering to the search engine.

## 2. Capability model

Capabilities are tri-state when based on documentation/configuration:

```text
true | false | unknown
```

Suggested capabilities:

- `search`
- `free_text_query`
- `title_filter`
- `author_filter`
- `narrator_filter`
- `series_filter`
- `isbn_filter`
- `asin_filter`
- `language_filter`
- `region_filter`
- `quick_search`
- `get_by_id`
- `bulk_get`
- `chapters`
- `author_search`
- `series_search`
- `native_sort`
- `native_pagination`
- `health`

Provider-specific extra capabilities MAY be exposed in a namespaced map.

## 3. Generic Audiobookshelf adapter

The standard ABS custom-provider contract is a search-only HTTP facade centered on:

```text
GET {base_url}/search?query=<text>&author=<optional>
AUTHORIZATION: <configured value>   # if configured
```

The response shape is:

```json
{
  "matches": [
    {
      "title": "...",
      "subtitle": "...",
      "author": "...",
      "narrator": "...",
      "publisher": "...",
      "publishedYear": "...",
      "description": "...",
      "cover": "https://...",
      "isbn": "...",
      "asin": "...",
      "genres": ["..."],
      "tags": ["..."],
      "series": [{"series": "...", "sequence": "..."}],
      "language": "...",
      "duration": 12345
    }
  ]
}
```

Only title is universally required by the referenced ABS contract. Other fields are optional. Duration is interpreted as seconds in the ABS contract.

### 3.1 Request mapping

Default mapping:

- `query`: use `request.query`, otherwise title, otherwise a concatenation of the strongest textual hints.
- `author`: map if supplied.
- Other structured fields: do not invent undocumented query parameters. Provider configuration MAY define supported extra query parameters or path parameters.

### 3.2 Path-parameter providers

Some ABS-compatible servers encode provider options into the base URL, for example an aggregator instance such as:

```text
https://provider.example/bookbeat/market:austria
```

The CLI treats the full configured URL as the provider base and appends `/search` only at request time.

## 4. Libex native adapter

The Libex adapter SHOULD use the richer native API rather than only its ABS compatibility endpoints.

Recommended search strategy:

1. Identifier query: direct `/book/{asin}` or bulk `/book` when ASIN is supplied.
2. Incomplete free-text query: `/quick-search?keywords=...` first, then `/search` if needed.
3. Structured query: `/search` using title, author, narrator, publisher, keywords/query, limit/page, and region where relevant.
4. Exact database/offline-ish mode MAY use `/db/book` when the user explicitly requests local-index behavior.

The adapter can advertise `get_by_id`, `chapters`, `author_search`, `series_search`, and richer filters.

## 5. AudioSilo Meta native adapter

AudioSilo Meta exposes both a native read-only API and an ABS facade. The native adapter SHOULD use `/api/v1/search`, `/api/v1/lookup`, and work/recording detail endpoints when available. The ABS facade remains a valid generic-provider configuration.

Native full-text matching supports whole words and word prefixes; it does not claim arbitrary mid-word substring matching. Local fuzzy ranking therefore remains useful.

## 6. Provider response

Conceptual response:

```text
ProviderSearchResponse
  candidates: SearchResult[]
  elapsed_ms: integer
  from_cache?: boolean
  warnings: string[]
  native_request_count: integer
  status: ok | empty | partial | error | timeout | rate_limited
```

Provider adapters SHOULD return an empty candidate list rather than raising for a documented "no results" response such as an upstream 404 used to mean no matches.

## 7. Errors

Normalized error kinds:

- `timeout`
- `dns`
- `tls`
- `http_auth`
- `http_rate_limit`
- `http_server`
- `http_client`
- `invalid_response`
- `unsupported_capability`
- `configuration`
- `cancelled`

Errors MUST include provider ID and a redacted message. Raw headers are never included by default.

## 8. Contract discovery

Automatic discovery is optional. If implemented, it MUST be conservative:

- A successful `/health` does not imply search capabilities.
- A field present in one response does not prove it is always present.
- Unknown capabilities stay unknown unless documented, configured, or reliably discovered.
- User configuration overrides MAY declare capabilities but should be marked as overrides in `providers show`.

## 9. Adding a new provider

A new provider requires either:

1. Configuration using an existing generic adapter (`abs`, `libex`, `audiosilo`, future `opds-native`, etc.), or
2. A new adapter implementing the interface and fixture-based contract tests.

No CLI command-tree change should be required merely to add a provider.
