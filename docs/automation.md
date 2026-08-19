# Automation and machine output

`bookmeta` is non-interactive and safe to use in scripts, pipes, cron jobs, and CI. It never prompts, reads stdin, or opens a browser.

## Human and machine output

Human-readable output is the default. Search and provider-list commands render tables; detail commands render labeled values.

Use `--json` to select the stable v1 JSON contract:

```sh
bookmeta search "Dune" --json
bookmeta providers list --json
bookmeta get libex:B08G9PRS1K --json
```

Top-level JSON documents carry `"schema_version": 1`. Search output conforms to the bundled [search response schema](tasks/initial/bookmeta-cli-spec/schemas/search-response.schema.json), which references the [search result schema](tasks/initial/bookmeta-cli-spec/schemas/search-result.schema.json).

Within schema v1, new optional fields may be added, but existing fields are not repurposed. A breaking change requires a new schema version.

## JSON Lines

`search --jsonl` writes one compact normalized result per line:

```sh
bookmeta search "Dune" --jsonl | jq -r '.title'
```

JSONL has no envelope. Provider diagnostics go to standard error so every standard-output line remains parseable JSON. `--json` and `--jsonl` cannot be combined.

## Raw provider data

Use `--raw` with JSON or JSONL when you also need each provider's original payload:

```sh
bookmeta search "Dune" --json --raw
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
bookmeta search "Dune" --json --strict
```

## Errors and diagnostics

Fatal errors leave standard output empty, write a concise message to standard error, and return a non-zero exit code. Expected usage and configuration errors also include a suggested recovery action.

Unexpected or directly surfaced provider failures may save a redacted diagnostic file under a `logs` directory beside the effective configuration file. The path is printed to standard error unless `--quiet` is active. Credentials and sensitive headers are not included.

Use `--verbose` to include the resolved configuration path, time limits, result limit, and provider selection on standard error.

## Color and terminals

Set `NO_COLOR` or pass `--no-color` to disable ANSI output. Machine output never depends on terminal width, TTY detection, or color support.

All commands are read-only. There are no confirmation prompts, `--yes`, or `--dry-run` modes because `bookmeta` does not create, update, or delete remote or local book data.

## Suggested contract checks

These checks cover the stable automation surface:

```sh
bookmeta --help
bookmeta providers list --no-color
bookmeta providers list --json | jq -e '.schema_version == 1'
bookmeta search "Dune" --json | jq -e '.results | type == "array"'
bookmeta search "Dune" --jsonl | jq -c . >/dev/null
```

The test suite also verifies that a representative list command is a human table by default, the same command emits valid versioned JSON with `--json`, invalid option combinations exit `2`, and strict provider failures preserve completed results.
