# bookmeta

`bookmeta` is a read-only .NET CLI for searching, ranking, and inspecting book and audiobook metadata across multiple provider instances. It has native adapters for Libex and AudioSilo, generated from their OpenAPI descriptions with Kiota, plus a generic Audiobookshelf-compatible custom-provider adapter.

The implementation follows the specification in [`docs/tasks/initial/bookmeta-cli-spec`](docs/tasks/initial/bookmeta-cli-spec/README.md).

## Build and run

.NET SDK 10 is required and pinned by `global.json`.

```sh
dotnet restore
dotnet build BookMeta.slnx
dotnet test BookMeta.slnx
dotnet run --project src/BookMeta.Cli -- --help
```

For local use, publish a single framework-dependent executable and put it on `PATH`:

```sh
dotnet publish src/BookMeta.Cli -c Release -r linux-x64 --self-contained false -p:PublishSingleFile=true
install -m 0755 src/BookMeta.Cli/bin/Release/net10.0/linux-x64/publish/bookmeta ~/.local/bin/bookmeta
```

## Configure providers

Start from the supplied example:

```sh
mkdir -p ~/.config/bookmeta
cp docs/tasks/initial/bookmeta-cli-spec/examples/config.toml ~/.config/bookmeta/config.toml
bookmeta config validate
bookmeta providers list
```

The default path is `$XDG_CONFIG_HOME/bookmeta/config.toml` or `~/.config/bookmeta/config.toml` on Linux, the Application Support directory on macOS, and `%APPDATA%\bookmeta\config.toml` on Windows.

A provider ID such as `libex` is the user-facing target selector. Its normalized full base URL, including a path prefix, is the network and credential identity boundary. Two instances with different base URLs should have different IDs. There is deliberately no separate account or profile store: credentials belong to a configured provider and are referenced explicitly:

```toml
[providers.private_abs]
type = "abs"
base_url = "https://metadata.example/api/provider"
auth = "env:BOOKMETA_PRIVATE_ABS_AUTH"

[providers.private_abs.headers]
X-Api-Key = "file:/run/secrets/bookmeta-api-key"
```

Supported secret references are `env:NAME`, `file:/path`, and `literal:value`. `literal:` is useful for local compatibility but `config validate` warns because the value is stored in plaintext. Secret values and sensitive headers are redacted from provider inspection and diagnostic logs. Plain HTTP is rejected except for loopback/private targets or an explicit `allow_insecure_http = true` opt-in.

Configuration precedence is deterministic:

1. Command flags override settings loaded from the file.
2. `--config PATH` selects the file; otherwise `BOOKMETA_CONFIG` selects it.
3. The platform config path is used when neither is set.
4. Omitted TOML settings use built-in defaults.

Provider selection has its own precedence: explicit `--provider` and `--group` selections win; otherwise the configured `default_group` is expanded; otherwise all enabled providers are used. `--exclude` is applied last. A disabled provider is never selected implicitly and produces an error when explicitly requested.

## Command tree

```text
bookmeta
├── search [QUERY]                 search, normalize, rank, and cluster results
├── get PROVIDER:ID               retrieve one provider-native record
├── providers
│   ├── list                      list configured provider instances
│   ├── show PROVIDER             inspect one redacted instance
│   ├── test [PROVIDER...]        run read-only connectivity checks
│   └── capabilities [PROVIDER...] show tri-state capability evidence
├── config
│   ├── path                      print the resolved TOML path
│   └── validate                  validate config, groups, URLs, and secrets
└── completion SHELL              emit bash, zsh, fish, or PowerShell setup
```

The top level contains the two primary data operations. Provider discovery and configuration diagnostics are grouped because they are secondary operational surfaces rather than search actions.

Common examples:

```sh
bookmeta search "har pot philos"
bookmeta search --title "Dune" --author "Frank Herbert" --explain
bookmeta search --isbn 9780441172719 --exact --json
bookmeta search "The Hobbit" --provider libex --provider audiosilo --editions
bookmeta search "Project Hail Mary" --group audiobook --exclude slow-provider --jsonl
bookmeta get libex:B08G9PRS1K --json
bookmeta providers test libex audiosilo --timeout 10s
bookmeta completion zsh
```

`search` fans out concurrently under per-provider timeouts and a global deadline. Results are normalized, locally ranked, and clustered into works and editions. `--editions` expands AudioSilo work cards through bounded work-detail requests; `--no-dedupe` returns individual candidates; `--exact` retains only exact identifiers or high-confidence field matches. Transient requests receive one bounded retry. Successful responses are cached on disk unless `--fresh` is supplied; authentication, rate-limit, and server failures are not cached.

## Output contract

Human-readable output is the default. Searches and provider lists render bounded tables; detail commands render labeled values. Markup and control characters received from providers are escaped. `--quiet` reduces human output but never removes requested machine data.

`--json` opts into a stable snake-case v1 document with an in-band `schema_version`. Search output conforms to [`search-response.schema.json`](docs/tasks/initial/bookmeta-cli-spec/schemas/search-response.schema.json), whose results reference [`search-result.schema.json`](docs/tasks/initial/bookmeta-cli-spec/schemas/search-result.schema.json). `get`, provider, and config JSON commands also emit a top-level `schema_version: 1` envelope. Additive fields may be introduced within v1; existing field meanings are not changed.

`search --jsonl` emits one compact normalized search result per line and is mutually exclusive with `--json`. It implies the v1 search-result shape but has no separate envelope. `--raw` is accepted only with JSON/JSONL and adds the bounded provider payload to each result.

Standard output contains only the requested primary data. Human warnings, recovery guidance, verbose diagnostics, and error messages go to standard error. Search JSON carries partial-provider warnings and statuses inside the response; fatal failures still use standard error plus the process exit code. Commands never prompt, read stdin, open a browser, or require a TTY, so redirected and CI execution behave the same as an interactive terminal. `NO_COLOR` and `--no-color` disable ANSI output.

## Errors and exit status

| Code | Meaning |
| ---: | --- |
| `0` | Success, including a non-strict search with usable partial results |
| `1` | Unexpected general failure |
| `2` | Invalid arguments or option combination |
| `3` | Invalid configuration or provider selection |
| `4` | All selected providers failed, or a provider connectivity test failed |
| `5` | `--strict` search had any provider failure |
| `6` | Requested capability is unsupported |
| `10` | Cancellation or deadline termination |

Errors include a concise message and, when possible, a `next:` recovery action. Unexpected and directly surfaced provider failures write a redacted diagnostic log under the config-adjacent `logs` directory; predictable usage and configuration errors do not. The path is reported unless `--quiet` is active. Use `--verbose` to print resolved configuration, limits, timeouts, and selected providers to standard error.

Every command is read-only. There are no create, update, delete, or remote side-effect operations, so confirmation and dry-run flags are intentionally absent.

## Regenerate Kiota clients

Kiota is pinned as a repository-local .NET tool. Libex generation uses the bundled OpenAPI source for reproducibility; AudioSilo generation uses its published OpenAPI document.

```sh
./eng/generate-clients.sh
dotnet build BookMeta.slnx
dotnet test BookMeta.slnx
```

Generated sources live under `src/BookMeta.Cli/Generated` and should be committed together with their Kiota lock files. Generic ABS providers do not publish one common schema, so that adapter uses a bounded, defensive HTTP/JSON transport instead.
