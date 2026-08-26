# Automation and machine output

`dotnet audiobookmeta` is non-interactive and safe to use in scripts, pipes, cron jobs, and CI. It never prompts, reads stdin, or opens a browser.

## Human and machine output

Human-readable output is the default. Search and provider-list commands render tables; detail commands render labeled values.

Use `--json` to select the stable v1 JSON contract:

```sh
dotnet audiobookmeta search "Dune" --json
dotnet audiobookmeta providers list --json
dotnet audiobookmeta get libex:B08G9PRS1K --json
dotnet audiobookmeta author books "Andy Weir" --provider libex --json
dotnet audiobookmeta config get search.limit --json
```

Top-level JSON documents carry `"schema_version": 1`. Search output conforms to the bundled [search response schema](tasks/initial/bookmeta-cli-spec/schemas/search-response.schema.json), which references the [search result schema](tasks/initial/bookmeta-cli-spec/schemas/search-result.schema.json).

Within schema v1, new optional fields may be added, but existing fields are not repurposed. A breaking change requires a new schema version.

Search results include an optional `lookup_strategy` describing how a provider found the record. Each `provider_status` includes `lookup_strategies`, including attempted strategies that returned no candidates. Libex values include `sku`, `quick_search`, `structured_search`, and `author_fallback`.

Identifier searches expose `identifier_match_kind` (`sku_exact`, `sku_group`, `asin_exact`, or `isbn_exact`) and a `match_assessment`. A `corroborated_identifier_match` agrees with supplied metadata; `identifier_match` has no corroborating fields; `conflicting_identifier_match` is downgraded and includes structured `conflicts` with requested and candidate values. Concrete SKU matches rank above same-group regional alternatives, and the requested or configured region breaks ties between group matches.

`author books --json` emits `{ "schema_version": 1, "request": {...}, "results": [...] }`. Its normalized results use the same result shape as search and get. `--raw` adds unversioned provider payloads and therefore requires `--json`.

## JSON Lines

`search --jsonl` writes one compact normalized result per line:

```sh
dotnet audiobookmeta search "Dune" --jsonl | jq -r '.title'
```

JSONL has no envelope. Provider diagnostics go to standard error so every standard-output line remains parseable JSON. `--json` and `--jsonl` cannot be combined.

## Raw provider data

Use `--raw` with JSON or JSONL when you also need each provider's original payload:

```sh
dotnet audiobookmeta search "Dune" --json --raw
```

Raw payloads are unversioned provider data and may change independently of the normalized v1 contract. Raw capture is size-limited and bypasses the normalized result cache.

## Standard streams

- Standard output contains only requested primary data: tables, values, JSON, or JSONL.
- Human warnings, recovery guidance, verbose diagnostics, and errors use standard error.
- Search JSON represents partial-provider warnings and provider statuses inside the document.
- JSONL keeps its direct-value stream on standard output and sends provider failures to standard error.
- `--verbose` writes only to standard error.
- `--quiet` suppresses non-essential human output but never suppresses requested machine output or changes success into failure.

No command emits JSON unless `--json` or `--jsonl` is explicitly supplied.

## Exit codes

| Code | Meaning |
| ---: | --- |
| `0` | Success, including zero matches or usable partial results in non-strict mode |
| `1` | Unexpected general failure |
| `2` | Invalid arguments or option combination |
| `3` | Invalid configuration or provider selection |
| `4` | All selected providers failed, or a connectivity test failed |
| `5` | `--strict` search had at least one provider failure |
| `6` | The requested provider capability is unsupported |
| `10` | The command was cancelled |

By default, one failed provider does not discard successful results or make the search fail. Add `--strict` when complete provider coverage is required:

```sh
dotnet audiobookmeta search "Dune" --json --strict
```

Provider-native no-result responses, including Libex HTTP 404 responses from search and author fallback endpoints, produce provider status `empty` and exit code `0`. Connectivity, authentication, rate-limit, malformed-response, and server errors remain provider failures.

## Errors and diagnostics

Fatal errors leave standard output empty, write a concise message to standard error, and return a non-zero exit code. Expected usage and configuration errors also include a suggested recovery action.

Unexpected or directly surfaced provider failures may save a redacted diagnostic file under a `logs` directory beside the effective configuration file. The path is printed to standard error unless `--quiet` is active. Credentials and sensitive headers are not included.

Use `--verbose` to include the resolved configuration path, time limits, result limit, and provider selection on standard error.

## Color and terminals

Set `NO_COLOR` or pass `--no-color` to disable ANSI output. Machine output never depends on terminal width, TTY detection, or color support.

Provider, search, and retrieval commands are read-only and never change remote services or local book data. `config set` performs an explicitly targeted local setting update without prompting. `config unset` requires `--yes`, supports `--dry-run`, and refuses the operation with exit code `2` when neither flag is supplied. No command reads standard input or opens an interactive prompt.

## Suggested contract checks

These checks cover the stable automation surface:

```sh
dotnet audiobookmeta --help
dotnet audiobookmeta providers list --no-color
dotnet audiobookmeta providers list --json | jq -e '.schema_version == 1'
dotnet audiobookmeta config get search.limit --json | jq -e '.schema_version == 1'
dotnet audiobookmeta search "Dune" --json | jq -e '.results | type == "array"'
dotnet audiobookmeta search "Dune" --jsonl | jq -c . >/dev/null
```

The test suite also verifies that a representative list command is a human table by default, the same command emits valid versioned JSON with `--json`, invalid option combinations exit `2`, and strict provider failures preserve completed results.
