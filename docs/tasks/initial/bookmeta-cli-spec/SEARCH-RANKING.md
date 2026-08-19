# Search, incomplete matching, ranking, and clustering

## 1. Principle

Providers discover candidates; the CLI ranks and groups them. Upstream search quality varies substantially, so incomplete-query support must not depend on every provider implementing fuzzy search.

## 2. Input normalization

For matching only (never overwrite displayed metadata), produce a normalized form:

1. Unicode normalization (NFKC recommended).
2. Unicode-aware case folding.
3. Normalize apostrophes/dashes and collapse whitespace.
4. Remove or space-separate punctuation that does not carry identifier meaning.
5. Preserve digits and series-number punctuation in an auxiliary form.
6. Strip common ISBN separators for identifier comparison.

Language-specific transliteration SHOULD be optional and MUST keep the original form alongside the transliterated form.

Example:

```text
"The Philosopher’s Stone" -> "the philosophers stone"
"J.R.R. Tolkien"          -> "j r r tolkien"
"978-0-261-10221-7"       -> "9780261102217"
```

## 3. Query expansion

Default incomplete-search expansion is deliberately bounded. For an input like:

```text
har pot philos
```

the engine creates matching evidence such as token prefixes:

```text
har* pot* philos*
```

It SHOULD NOT blindly generate every edit-distance variant and flood providers.

Safe upstream variants may include:

- original free-text query;
- title-only variant when a provider distinguishes title and general query;
- quick/suggestion endpoint where supported;
- retry without author when a provider adapter documents this as useful;
- normalized ISBN/ASIN lookup when identifiers are detected.

Default maximum upstream search requests per provider for one user search SHOULD be 2, unless a provider adapter explicitly documents a multi-request search flow.

## 4. Local text similarity

Recommended signals:

- exact normalized equality;
- token-prefix coverage;
- ordered-token coverage;
- token-set overlap;
- edit similarity on complete strings;
- edit similarity per token;
- numeric/series-position agreement.

Prefix evidence matters more than arbitrary substring evidence for incomplete input. `phil` matching `philosopher` is stronger than matching an unrelated token containing `phil` in the middle.

## 5. Ranking

Ranking is evidence-based and should avoid punishing a provider merely for omitting an optional field. Compute a weighted score over evidence applicable to the request.

Suggested maximum contributions:

| Evidence | Weight |
|---|---:|
| Exact ISBN/ASIN match | decisive / 100 |
| Title similarity | 55 |
| Author similarity | 20 |
| Series similarity | 8 |
| Narrator similarity | 7 |
| Language/region match | 5 |
| Edition hints (publisher/year) | 5 |

When an identifier matches exactly, textual differences should not normally demote the candidate below non-identifier matches, but conflicting identifiers MUST be surfaced.

### 5.1 Title score

A practical title score can combine:

```text
0.40 token-prefix coverage
0.30 ordered-token coverage
0.20 whole-string edit similarity
0.10 token-set similarity
```

For a full exact normalized title, force the title component to 1.0.

### 5.2 Author score

If the user supplied an author, author evidence is mandatory for a top confidence label. A high title score with a poor author score can still be shown but should be marked ambiguous.

### 5.3 Provider quality

Do not hardcode popularity as truth. Provider priority is a tie-breaker/configuration preference, not a substitute for metadata evidence.

## 6. Confidence labels

Optional human labels:

- `exact` — strong identifier or exact title+author evidence.
- `high` — score >= 90 with no material conflict.
- `medium` — 75–89.
- `low` — below 75.

Thresholds are proposed defaults and should be configurable only if there is a concrete use case.

## 7. Deduplication and clustering

### 7.1 Recording/edition cluster

Treat records as the same recording/edition when strong evidence agrees, in descending priority:

1. Same provider-independent edition identifier (ASIN/ISBN where appropriate).
2. Same normalized title + author + narrator + compatible publication data.
3. Explicit provider-native cross-region/SKU grouping, when trustworthy.

Do not merge records with conflicting non-empty ASINs merely because title and author match.

### 7.2 Work cluster

Work-level grouping may combine editions/recordings using:

1. shared work identifier if a provider supplies one;
2. normalized title + author with very high similarity;
3. series/position agreement as supporting evidence.

Different narrators can coexist inside one work group as separate recordings.

### 7.3 Display behavior

Default interactive output shows work groups. The group's displayed metadata is selected, not merged blindly:

- title/author from the highest-ranked member;
- source list from all members;
- narrator shown only if unambiguous, otherwise `multiple`;
- identifiers remain attached to member editions.

`--editions` shows each edition/recording. `--no-dedupe` disables both clustering layers for display.

## 8. Explain mode

`--explain` SHOULD show compact evidence, for example:

```text
97 libex  Project Hail Mary
   title: 1.00 exact/prefix
   author: 1.00 "weir" -> "Andy Weir"
   query path: quick-search + search
   cluster: work:project-hail-mary|andy-weir
```

Do not expose secrets, full signed URLs, or sensitive headers.

## 9. Provider-specific incomplete-search notes

- **Libex**: documented quick-search/suggestion endpoint is preferred for partial input.
- **AudioSilo Meta**: native FTS supports whole words and word prefixes, not arbitrary mid-word substring matching.
- **abs-storytel-provider**: repository documents author relevance ranking and retry without author when combined search yields no results.
- **abs-agg**: search behavior differs by subprovider; the CLI should use local reranking consistently.
- **Generic ABS providers**: incomplete support is unknown unless their own docs say otherwise; local ranking compensates after candidate retrieval.
