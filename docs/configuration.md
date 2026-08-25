# Configuration and providers

`dotnet audiobookmeta` reads a TOML file that describes metadata providers and search defaults. It never modifies this file automatically.

## Configuration location

Print the effective path with:

```sh
dotnet audiobookmeta config path
```

The default is:

| Platform | Path |
| --- | --- |
| Linux | `$XDG_CONFIG_HOME/bookmeta/config.toml`, or `~/.config/bookmeta/config.toml` |
| macOS | `~/Library/Application Support/bookmeta/config.toml` |
| Windows | `%APPDATA%\bookmeta\config.toml` |

The `bookmeta` directory name and `BOOKMETA_CONFIG` environment variable are retained for compatibility with existing installations.

Use `--config PATH` for one command or set `BOOKMETA_CONFIG` to choose another file.

Resolution order is:

1. `--config PATH`
2. `BOOKMETA_CONFIG`
3. The platform default path

Command flags override matching values loaded from the file. Missing settings use built-in defaults.

## A complete starting configuration

```toml
version = 1
default_group = "default"

[search]
limit = 10
limit_per_provider = 10
provider_timeout = "4s"
deadline = "8s"
max_concurrency = 8
cache_ttl = "15m"

[providers.libex]
type = "libex"
base_url = "https://libexdb.com"
enabled = true
region = "us"
priority = 100
groups = ["default", "audiobook"]

[providers.audiosilo]
type = "audiosilo"
base_url = "https://meta.audiosilo.app"
enabled = true
priority = 95
groups = ["default", "audiobook", "open-data"]

[groups]
default = ["libex", "audiosilo"]
audiobook = ["libex", "audiosilo"]
open-data = ["audiosilo"]
```

Provider IDs such as `libex` and `audiosilo` are the names used by `--provider`, `providers show`, and `get`.

## Search settings

| Setting | Default | Meaning |
| --- | ---: | --- |
| `limit` | `10` | Maximum displayed works or editions |
| `limit_per_provider` | `10` | Candidate budget requested from each provider |
| `provider_timeout` | `4s` | Time allowed for one provider |
| `deadline` | `8s` | Time allowed for the whole search |
| `max_concurrency` | `8` | Maximum providers contacted at once |
| `cache_ttl` | `15m` | Lifetime of cached normalized search results |

Durations accept `ms`, `s`, `m`, and `h`, for example `500ms`, `10s`, or `1m`.

## Provider types

### Libex

Libex supplies Audible-oriented metadata and supports broad search, quick suggestions, structured filters, native paging, dedicated author-book lookup, and direct ASIN retrieval.

```toml
[providers.libex]
type = "libex"
base_url = "https://libexdb.com"
enabled = true
region = "us"
```

### AudioSilo

AudioSilo supplies work and audiobook-recording metadata and supports ASIN/ISBN lookup and edition expansion.

```toml
[providers.audiosilo]
type = "audiosilo"
base_url = "https://meta.audiosilo.app"
enabled = true
```

### Audiobookshelf-compatible providers

Use `type = "abs"` for a provider implementing the Audiobookshelf custom metadata-provider search contract. The base URL normally excludes the final `/search` segment.

```toml
[providers.my_catalog]
type = "abs"
base_url = "https://metadata.example/catalog"
enabled = true
groups = ["default", "books"]
```

Some providers encode options in their path. Preserve the entire documented path in `base_url`:

```toml
[providers.storytel_en]
type = "abs"
base_url = "https://metadata.example/storytel/language:en"
enabled = true
```

Set `append_search_path = false` only when a proxy or provider explicitly expects the configured URL to be used without appending `/search`.

## Credentials and custom headers

Credentials belong to a specific provider. Reference them instead of placing a secret directly in normal configuration:

```toml
[providers.private_catalog]
type = "abs"
base_url = "https://metadata.example/private"
auth = "env:BOOKMETA_PRIVATE_AUTH"

[providers.private_catalog.headers]
X-Api-Key = "file:/run/secrets/audiobookmeta-api-key"
```

Supported secret references are:

| Form | Meaning |
| --- | --- |
| `env:NAME` | Read the value from an environment variable |
| `file:/path` | Read the value from a file and trim its final newline |
| `literal:value` | Store the value directly in TOML |

`literal:` is supported for compatibility but stores plaintext. `dotnet audiobookmeta config validate` warns when it is used. Secret values and sensitive headers are redacted from provider inspection and diagnostic logs.

Use `auth` for the complete `Authorization` header value. An `Authorization` entry inside `headers` is rejected.

## Groups and provider selection

A provider can join groups through its `groups` array or the top-level `[groups]` table. Duplicate membership is removed automatically.

With no selection option, `search` uses `default_group` when configured; otherwise it uses every enabled provider. Explicit `--provider` and `--group` options replace that default selection, and `--exclude` is applied last.

```sh
dotnet audiobookmeta search "Dune" --provider libex
dotnet audiobookmeta search "Dune" --group audiobook
dotnet audiobookmeta search "Dune" --group audiobook --exclude slow-provider
dotnet audiobookmeta search "Dune" -p @audiobook
```

Groups may include another group with `@name`. Recursive groups, missing members, unknown providers, and explicitly selected disabled providers are errors rather than silent fallbacks.

## Advanced provider settings

```toml
[providers.custom]
type = "abs"
base_url = "https://metadata.example/catalog"
enabled = true
priority = 50
timeout = "6s"

[providers.custom.query_params]
market = "us"

[providers.custom.headers]
X-Client = "audiobookmeta"

[providers.custom.capabilities]
isbn_filter = true
not_found_is_empty = true
```

- `priority` is a deterministic provider tie-breaker, not a replacement for relevance scoring.
- `timeout` overrides the default timeout for this provider.
- `query_params` adds documented static query parameters.
- `headers` adds request headers; values may be secret references.
- `capabilities` explicitly declares unusual deployment behavior as `true`, `false`, or `"unknown"`.

## HTTP and redirect safety

HTTPS is required for public provider URLs. Loopback and private-network HTTP targets are allowed; other plain HTTP targets require an explicit opt-in:

```toml
allow_insecure_http = true
```

Authenticated redirects to another host are refused by default so credentials cannot silently cross a provider boundary. Set `allow_cross_host_redirects = true` only when you control and trust both endpoints.

## Validate and inspect

```sh
dotnet audiobookmeta config validate
dotnet audiobookmeta providers list
dotnet audiobookmeta providers show PROVIDER
dotnet audiobookmeta providers capabilities PROVIDER
dotnet audiobookmeta providers test PROVIDER --timeout 10s
```

`providers show` displays the effective provider configuration with credentials redacted. `providers capabilities` distinguishes supported, unsupported, and unknown behavior.
