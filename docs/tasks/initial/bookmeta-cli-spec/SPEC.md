# bookmeta CLI — normative specification

## 1. Goals

`bookmeta` searches book and audiobook metadata from one or more configured providers through a single CLI. It MUST:

1. Accept incomplete, abbreviated, or imperfect search strings and rank likely matches locally.
2. Allow the caller to select one, several, a group, or all enabled providers.
3. Support generic Audiobookshelf custom metadata providers without provider-specific code when they implement the standard `GET /search` contract.
4. Support richer native adapters when a provider exposes materially better capabilities, initially Libex and AudioSilo Meta.
5. Preserve source provenance and raw provider identity for every candidate.
6. Avoid treating all editions/recordings as one record. Work-level grouping and recording/edition-level identity MUST remain distinguishable.
7. Be useful interactively and in scripts through stable JSON/JSONL output.
8. Continue when one provider fails unless strict mode is requested.

## 2. Non-goals for v1

The first release does not need to edit Audiobookshelf libraries, download covers, modify tags, write metadata into media files, or reconcile provider data into a single authoritative record. Those can be separate future commands.

## 3. Command tree

```text
bookmeta
├── search [QUERY]
├── get PROVIDER:ID
├── providers
│   ├── list
│   ├── show PROVIDER
│   ├── test [PROVIDER...]
│   └── capabilities [PROVIDER...]
├── config
│   ├── path
│   └── validate
└── completion <shell>
```

`search` is the primary command. Implementations MAY add aliases, but MUST keep the documented forms stable.

## 4. Search command

### 4.1 Invocation

```bash
bookmeta search "project hail"
bookmeta search "proj hail" --author weir
bookmeta search --title "The Hobbit" --author Tolkien
bookmeta search "dune" -p libex -p audiosilo
bookmeta search "dune" -p @audiobook
bookmeta search "dune" --exclude goodreads
bookmeta search "dun" --exact
bookmeta search "dune" --json
```

At least one of positional `QUERY`, `--title`, `--author`, `--narrator`, `--isbn`, `--asin`, or `--series` MUST be supplied.

### 4.2 Search flags

| Flag | Meaning |
|---|---|
| `-p, --provider ID` | Include provider instance. Repeatable. |
| `--exclude ID` | Exclude provider instance. Repeatable. |
| `--group NAME` | Include configured group; `-p @NAME` is equivalent. |
| `--title TEXT` | Structured title hint. |
| `--author TEXT` | Structured author hint. |
| `--narrator TEXT` | Structured narrator hint. |
| `--series TEXT` | Structured series hint. |
| `--isbn VALUE` | ISBN hint; normalize punctuation before sending/ranking. |
| `--asin VALUE` | ASIN hint. |
| `--language CODE` | Preferred/required language depending on provider capability. |
| `--region CODE` | Region/market hint. |
| `--limit N` | Maximum displayed normalized groups, default 10. |
| `--limit-per-provider N` | Candidate budget per provider, default 10. |
| `--timeout DURATION` | Per-provider timeout override. |
| `--deadline DURATION` | Whole-search deadline. |
| `--exact` | Disable query expansion and fuzzy local matching; still normalize case/punctuation. |
| `--fresh` | Bypass CLI cache where possible. |
| `--no-dedupe` | Show every provider candidate independently. |
| `--editions` | Expand recording/edition candidates instead of work-group summary. |
| `--raw` | Include provider-native response payloads in JSON output when available. |
| `--explain` | Show ranking evidence and provider warnings. |
| `--strict` | Any selected-provider failure makes the command non-zero. |
| `--json` | Emit one JSON document. |
| `--jsonl` | Emit one normalized candidate/group per line plus diagnostics on stderr. |
| `--quiet` | Print only identifiers/titles appropriate to the selected output mode. |

`--json` and `--jsonl` are mutually exclusive.

### 4.3 Provider selection rules

1. With no selection flag, search all enabled providers in the default group if one is configured; otherwise all enabled providers.
2. One or more `--provider` flags restrict the search to those providers/groups.
3. `--exclude` is applied after group expansion.
4. Unknown provider IDs are usage/configuration errors; they MUST NOT be silently ignored.
5. Disabled providers explicitly selected by ID SHOULD produce a clear error unless `--allow-disabled` is added by an implementation extension.

### 4.4 Incomplete query behavior

Incomplete matching is ON by default for free-text and structured textual fields. The CLI MUST NOT rely solely on upstream fuzzy behavior. It SHOULD:

1. Normalize the input.
2. Prefer provider-native suggestion/quick-search endpoints when documented.
3. Run the provider's ordinary search.
4. Optionally issue a small number of safe query variants when the provider benefits from them.
5. Normalize returned records.
6. Rank candidates locally using prefix/token/fuzzy evidence.

Example: `har pot philos` should be able to rank `Harry Potter and the Philosopher's Stone` highly even if the provider only returns it as a broad search candidate.

`--exact` disables steps 2–4 except a provider-native exact/identifier lookup and uses exact/prefix-free scoring.

## 5. Get command

```bash
bookmeta get libex:B00B7NPRY8
bookmeta get audiosilo:work/harry-potter-and-the-philosophers-stone
```

`get` requires a provider that advertises `get_by_id` or a provider-specific resolvable ID kind. Generic ABS search-only providers SHOULD return an unsupported-capability error rather than fabricate a lookup.

## 6. Provider commands

### 6.1 `providers list`

Shows configured instance ID, adapter type, enabled state, groups, base host, and a compact capability summary. Secrets MUST never be printed.

### 6.2 `providers show`

Shows configuration after secret redaction, capabilities, provider-specific parameters, and source notes.

### 6.3 `providers test`

Performs only non-destructive connectivity/contract checks. If a provider lacks a health endpoint, it MAY run a minimal search fixture. Tests MUST use short timeouts and MUST NOT reveal authorization headers.

### 6.4 `providers capabilities`

Outputs documented and discovered capabilities. The CLI MUST distinguish `true`, `false`, and `unknown`; unknown MUST NOT be presented as false.

## 7. Normalized search request

Normative logical model:

```text
SearchRequest
  query?: string
  title?: string
  author?: string
  narrator?: string
  series?: string
  isbn?: string
  asin?: string
  language?: string
  region?: string
  limit_per_provider: integer
  exact: boolean
```

Adapters MAY ignore unsupported optional fields, but MUST report that omission in diagnostic metadata when `--explain` is enabled. In strict-capability mode (future extension), unsupported supplied fields MAY become errors.

## 8. Normalized candidate

Every provider result MUST normalize to a `SearchResult` carrying:

- `provider`: configured provider instance ID.
- `provider_type`: adapter type.
- `provider_record_id`: provider-native stable ID when one exists.
- `title`, `subtitle`.
- `authors[]`, `narrators[]`.
- `series[]` entries with name and optional sequence.
- identifiers: `isbn10[]`, `isbn13[]`, `asin[]`, plus provider-specific identifiers.
- `publisher`, `published_year`, `release_date` where available.
- `language`, `duration_seconds`.
- `genres[]`, `tags[]`.
- `cover_url`, `description`.
- `score` and optional `score_explanation`.
- `source_url` when supplied by the provider.
- `raw` only when requested.
- `warnings[]`.

Missing metadata is represented as null/empty, not guessed.

## 9. Work and edition/recording identity

The CLI MUST preserve two conceptual levels:

- **Work**: the abstract intellectual book, primarily title + author + work-level identifiers.
- **Edition/recording**: a specific publication or narration, which may differ by narrator, publisher, date, region, ISBN, or ASIN.

By default, search results MAY be grouped at work level for readability, but each group MUST retain the member candidates. `--editions` MUST expose individual normalized candidates.

The CLI MUST NOT merge two recordings solely because their titles and authors match when strong edition-level evidence conflicts (for example, different narrators plus different ASINs).

## 10. Output

### 10.1 Human table

Default columns:

```text
#  Score  Title                    Author       Narrator      Sources
1  99     Project Hail Mary        Andy Weir    Ray Porter    libex,audiosilo
2  91     Project Hail Mary ...    Andy Weir    —             goodreads
```

Columns MAY adapt to terminal width. Provider failures appear after results as concise warnings.

### 10.2 JSON

JSON output MUST be versioned with a top-level `schema_version`. It includes:

```json
{
  "schema_version": 1,
  "request": {},
  "results": [],
  "provider_status": [],
  "warnings": []
}
```

Field names and shapes are specified in `schemas/`.

## 11. Exit codes

Recommended stable codes:

| Code | Meaning |
|---|---|
| `0` | Command executed successfully, including zero matches and partial provider failure in non-strict mode. |
| `2` | Usage error. |
| `3` | Configuration error. |
| `4` | All selected providers failed or no selected provider could run. |
| `5` | `--strict` and one or more selected providers failed. |
| `6` | Unsupported capability for a command such as `get`. |

Implementations SHOULD avoid reusing these codes for unrelated conditions.

## 12. Performance requirements

- Independent providers SHOULD execute concurrently.
- Default per-provider timeout SHOULD be 4 seconds; configuration may override.
- Default global search deadline SHOULD be 8 seconds.
- One failed or slow provider MUST NOT delay completed results past the global deadline.
- Retries are allowed only for idempotent operations and SHOULD honor `Retry-After`.
- The CLI SHOULD cap concurrent provider requests (suggested default: 8).

## 13. Security requirements

1. Authorization secrets MUST support environment-variable references.
2. Secrets MUST never appear in normal logs, `providers show`, error URLs, or JSON diagnostics.
3. HTTPS SHOULD be required for non-local public endpoints unless explicitly overridden.
4. Community providers are third-party software; the CLI MUST treat their responses as untrusted data.
5. HTML in descriptions MUST NOT be interpreted/executed by the terminal renderer.
6. Redirects that change host SHOULD be disabled by default for authenticated requests or require explicit opt-in.
7. Raw response capture, when requested, SHOULD apply a size limit.

## 14. Compatibility policy

The CLI's normalized JSON schema is versioned independently from provider APIs. Provider adapters absorb upstream changes. Breaking changes to `schema_version: 1` require a new schema version rather than silent field reinterpretation.

## 15. Acceptance examples

### Incomplete title

```bash
bookmeta search "har pot philos"
```

Expected: likely Harry Potter matches rank above unrelated books even if the upstream provider returned a broad candidate set.

### Specific providers

```bash
bookmeta search "dune" -p libex -p audiosilo
```

Expected: only those two configured instances are called.

### Provider group

```bash
bookmeta search "dune" -p @audiobook
```

Expected: group expansion is deterministic and shown with `--explain`.

### Failure isolation

If 4 providers succeed and 1 times out, default mode returns results with exit 0 and a warning. `--strict` returns the same results but exits 5.
