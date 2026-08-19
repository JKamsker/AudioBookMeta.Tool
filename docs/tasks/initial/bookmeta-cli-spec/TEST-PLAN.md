# Test plan

## 1. Unit tests

### Query normalization

- Unicode case folding.
- Curly/straight punctuation normalization.
- ISBN separator removal.
- Numeric series tokens retained.
- Empty/whitespace input rejected appropriately.

### Ranking

- `har pot philos` ranks `Harry Potter and the Philosopher's Stone` above unrelated titles.
- Exact title beats fuzzy title when author evidence is equal.
- Exact identifier dominates text-only candidates.
- Strong title + conflicting supplied author is not labeled exact/high without warning.
- Missing optional provider metadata does not automatically zero a score.

### Clustering

- Same ISBN clusters at edition level.
- Same title/author with different narrator and ASIN can share work cluster but remain separate editions.
- Conflicting ASINs are never silently collapsed into one edition.

## 2. Adapter fixture tests

### Generic ABS

Fixtures for:

- complete match;
- minimal title-only match;
- optional null/missing fields;
- malformed `matches` shape;
- 401;
- 429;
- 500;
- timeout;
- 404 documented-as-empty override.

### Libex

Use the supplied OpenAPI document to generate/maintain fixtures for:

- `/search`;
- `/quick-search`;
- `/book/{asin}`;
- bulk `/book`;
- `/book/{asin}/chapters`;
- authors/series if enabled in CLI;
- validation errors.

### AudioSilo

Fixtures for native search, identifier lookup, multiple recordings for one work, and ABS facade response.

## 3. Configuration tests

- env secret resolves;
- unresolved env secret produces config error before sending request;
- cyclic group rejected;
- provider in multiple groups deduplicated;
- `--exclude` wins after group expansion;
- invalid plain HTTP public endpoint requires explicit opt-in.

## 4. Concurrency tests

Use fake providers with deterministic delays:

- results arrive without sequential wait;
- one timeout does not cancel other successful providers;
- global deadline cancels unfinished tasks;
- max concurrency respected;
- strict mode changes exit code but preserves completed result output.

## 5. Security tests

- auth secret never appears in logs/errors/JSON;
- redirect to a different host does not forward auth by default;
- terminal control characters in metadata are escaped/sanitized;
- oversized raw response is truncated/rejected safely.

## 6. Acceptance tests

```text
A. bookmeta search "har pot philos"
   -> returns ranked plausible matches from configured providers.

B. bookmeta search "dune" -p libex -p audiosilo
   -> network spy proves no other provider was called.

C. one provider returns 500
   -> default exit 0 if another provider succeeds; warning present.

D. same as C with --strict
   -> exit 5; successful results still emitted.

E. JSON output validated against schemas/search-response.schema.json.
```
