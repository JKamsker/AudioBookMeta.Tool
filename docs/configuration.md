# Configuration and providers

`dotnet audiobookmeta` uses a TOML file that describes metadata providers and search defaults. On first
use of the platform-default path, it creates a ready-to-use file with the public Libex, AudioSilo, and
Lismio providers. Only Libex is selected by default. You can edit the file directly or manage supported
values through the `config` commands.

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

Normal read and provider commands do not create a missing custom `--config` or `BOOKMETA_CONFIG` path, which helps catch path typos. An explicit `config set ... --config PATH` command does create and seed that file because it is a configuration mutation.

Resolution order is:

1. `--config PATH`
2. `BOOKMETA_CONFIG`
3. The platform default path

Command flags override matching values loaded from the file. Missing settings use built-in defaults.

## Configure through the CLI

Read and update values with dot-separated keys:

```sh
dotnet audiobookmeta config get search.limit
dotnet audiobookmeta config set search.limit 20
dotnet audiobookmeta config set default_group audiobook
dotnet audiobookmeta config set groups.audiobook libex,audiosilo
dotnet audiobookmeta config validate
```

Provider fields use `providers.ID.FIELD`. Nested headers, query parameters, and capability overrides add one more segment:

```sh
dotnet audiobookmeta config set providers.catalog.type abs
dotnet audiobookmeta config set providers.catalog.base_url https://metadata.example/catalog
dotnet audiobookmeta config set providers.catalog.enabled true
dotnet audiobookmeta config set providers.catalog.groups default,books
dotnet audiobookmeta config set providers.catalog.headers.X-Client audiobookmeta
dotnet audiobookmeta config set providers.catalog.capabilities.isbn_filter true
dotnet audiobookmeta config validate
```

Strings are passed without TOML quotes. Provider and group lists use comma-separated values. Integers, booleans, durations, URLs, and capability states are checked before the file is changed. Each write is atomic. A provider can be built across several `config set` calls; run `config validate` after the required fields have been supplied.

Remove a key or a whole provider only with explicit confirmation, or preview the operation first:

```sh
dotnet audiobookmeta config unset providers.catalog --dry-run
dotnet audiobookmeta config unset providers.catalog --yes
```

These commands never prompt or read standard input. Human results go to standard output, errors go to standard error, and `--json` returns the versioned JSON v1 contract. `config get` redacts auth and sensitive header values in both output modes. Prefer `env:NAME` or `file:/path` secret references; command-line values can be retained by shell history and process inspection.

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
groups = ["audiobook", "open-data"]

[providers.lismio]
type = "lismio"
base_url = "https://lismio.app"
enabled = true
region = "de"
priority = 90
groups = ["shop-links"]

[groups]
default = ["libex"]
audiobook = ["libex", "audiosilo"]
open-data = ["audiosilo"]
shop-links = ["lismio"]
```

Provider IDs such as `libex`, `audiosilo`, and `lismio` are the names used by `--provider`, `providers show`, and `get`.

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

### Lismio

Lismio supplies public catalogue metadata and direct listening or purchasing links. Normal searches use
one catalogue request and return only fields present on its result cards. `--shop-links` explicitly
hydrates each result so output can include authors, narrators, contributor roles, series, publisher,
release date, duration, EAN, abridged state, descriptions, collections, versions, and shop URLs.

```toml
[providers.lismio]
type = "lismio"
base_url = "https://lismio.app"
enabled = true
region = "de"
timeout = "15s"
```

`region` selects the Lismio catalogue locale and defaults to `de`. The CLI recognizes links for Amazon
Music, Apple Books, Apple Music, Audible, BookBeat, Deezer, Everand, Google Play Books, Kobo, Nextory,
OverDrive, Spotify, Storytel, Thalia, and YouTube Music. Unrecognized external shop URLs are retained as
`unknown`. Shop-link hydration can make one detail request per candidate, with at most four running at
once. Keep `--limit-per-provider` small and increase both `--timeout` and `--deadline` when needed.

```sh
dotnet audiobookmeta search "Project Hail Mary" --provider lismio
dotnet audiobookmeta search "Project Hail Mary" --provider lismio --shop-links \
  --limit-per-provider 3 --timeout 15s --deadline 20s --json
dotnet audiobookmeta get lismio:38299
```

Provider selection remains configuration-driven. A default group that omits Lismio keeps it out of
ordinary searches, as in the starting configuration above. Explicit `--provider lismio` or
`--group shop-links` selects it. Add it to the default with
`dotnet audiobookmeta config set groups.default libex,lismio`. With no `default_group`, every enabled
provider is selected.

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
dotnet audiobookmeta config get search.deadline
dotnet audiobookmeta config set search.deadline 12s
dotnet audiobookmeta providers list
dotnet audiobookmeta providers show PROVIDER
dotnet audiobookmeta providers capabilities PROVIDER
dotnet audiobookmeta providers test PROVIDER --timeout 10s
```

`providers show` displays the effective provider configuration with credentials redacted. `providers capabilities` distinguishes supported, unsupported, and unknown behavior.
