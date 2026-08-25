# Configuration specification

## 1. Location

Default path by platform conventions:

- Linux: `$XDG_CONFIG_HOME/bookmeta/config.toml` or `~/.config/bookmeta/config.toml`
- macOS: `~/Library/Application Support/bookmeta/config.toml` MAY be supported, with XDG accepted.
- Windows: `%APPDATA%\\bookmeta\\config.toml`

`BOOKMETA_CONFIG` overrides the path.

## 2. Example

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
region = "de"
priority = 100
groups = ["default", "audiobook"]

[providers.audiosilo]
type = "audiosilo"
base_url = "https://meta.audiosilo.app"
enabled = true
priority = 95
groups = ["default", "audiobook", "open-data"]

[providers.goodreads]
type = "abs"
base_url = "https://provider.vito0912.de/goodreads"
auth = "literal:abs"
enabled = true
priority = 70
groups = ["default", "books"]

[providers.bookbeat_at]
type = "abs"
base_url = "https://provider.vito0912.de/bookbeat/market:austria"
auth = "literal:abs"
enabled = false
groups = ["audiobook"]

[providers.private_storytel]
type = "abs"
base_url = "http://abs-storytel-provider:3000/en/audiobook"
auth = "env:STORYTEL_PROVIDER_AUTH"
enabled = true
groups = ["audiobook"]
allow_insecure_http = true

[groups]
default = ["libex", "audiosilo", "goodreads"]
audiobook = ["libex", "audiosilo", "bookbeat_at", "private_storytel"]
books = ["goodreads", "audiosilo"]
open-data = ["audiosilo"]
```

## 3. Provider instance fields

| Field | Type | Meaning |
|---|---|---|
| `type` | string | Adapter type, e.g. `abs`, `libex`, `audiosilo`. |
| `base_url` | URL | Full provider base. For ABS providers do not include the final `/search`. |
| `enabled` | bool | Search by default/group when true. |
| `auth` | secret ref | Optional authorization header value. |
| `region` | string | Adapter-specific default region. |
| `priority` | integer | Tie-breaker/preference, not primary relevance. |
| `groups` | string[] | Convenience membership. |
| `timeout` | duration | Per-provider override. |
| `allow_insecure_http` | bool | Required for non-local plain HTTP if implementation enforces HTTPS. |
| `query_params` | table | Static query parameters for providers documented to accept them. |
| `headers` | table | Additional non-secret headers; secret values SHOULD use references. |
| `capabilities` | table | Explicit overrides for unusual/custom deployments. |

## 4. Secret references

Supported forms SHOULD include:

```text
env:NAME
file:/path/to/secret
literal:value
```

`literal:` is convenient but SHOULD trigger a warning because it stores the secret in plaintext config.

A future OS keychain reference MAY be added as `keychain:service/account`.

## 5. ABS provider options

Do not invent query parameters for generic ABS providers. Use the provider's base URL to encode path options when that provider documents path-segment parameters.

Example `abs-agg` BookBeat:

```toml
[providers.bookbeat_at]
type = "abs"
base_url = "https://provider.vito0912.de/bookbeat/market:austria"
auth = "literal:abs"
```

Example `abs-opds` (self-hosted PHP facade):

```toml
[providers.flibusta]
type = "abs"
base_url = "http://127.0.0.1:8000/?opds=https://example.invalid/opds&do="
allow_insecure_http = true
```

Because `abs-opds` uses a nonstandard URL template, an implementation MAY need an `append_search_path = false` escape hatch if its proxy already transforms paths/query arguments. This should be explicit, not guessed.

## 6. Capability overrides

Custom deployments may expose more or less than upstream documentation. Example:

```toml
[providers.my_provider.capabilities]
quick_search = false
isbn_filter = true
```

Overrides MUST be labeled `configured` in capability output.

## 7. Validation

`bookmeta config validate` MUST detect:

- duplicate provider IDs;
- invalid URLs;
- unresolved group members;
- recursive groups;
- missing required adapter fields;
- invalid secret-ref syntax;
- impossible timeouts/limits;
- insecure public HTTP when not explicitly allowed.
