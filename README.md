# AudioBookMeta.Tool

[![CI](https://github.com/JKamsker/AudioBookMeta.Tool/actions/workflows/ci.yml/badge.svg)](https://github.com/JKamsker/AudioBookMeta.Tool/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AudioBookMeta.Tool?logo=nuget&label=NuGet)](https://www.nuget.org/packages/AudioBookMeta.Tool)
[![NuGet downloads](https://img.shields.io/nuget/dt/AudioBookMeta.Tool?logo=nuget&label=downloads)](https://www.nuget.org/packages/AudioBookMeta.Tool)

Search book and audiobook metadata from several providers with one command.

`dotnet audiobookmeta` is useful when you know only part of a title, want to compare results from different sources, or need a stable JSON result for a script. It ranks imperfect matches locally, keeps different audiobook recordings separate, and continues when one provider is temporarily unavailable.

It is read-only: it does not edit your Audiobookshelf library, media files, or provider data.

## Supported providers

| Provider | Configuration type | Search | Direct retrieval | Notable support |
| --- | --- | --- | --- | --- |
| [Libex](docs/configuration.md#libex) | `libex` | Yes | ASIN | SKU/UFID lookup, duration metadata, native pagination, and author fallback |
| [AudioSilo Meta](docs/configuration.md#audiosilo) | `audiosilo` | Yes | Work ID, ISBN, or ASIN | Work and recording metadata, identifier lookup, and edition expansion |
| [Lismio](docs/configuration.md#lismio) | `lismio` | Yes | Catalogue record ID | Native pagination, locale-specific catalogues, and opt-in shop-link hydration |
| [Audiobookshelf-compatible providers](docs/configuration.md#audiobookshelf-compatible-providers) | `abs` | Yes | No | Generic support for the Audiobookshelf custom metadata-provider search contract; available fields vary by provider |

## Install

Install the NuGet tool artifact produced by CI with the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0):

```sh
dotnet tool install --global --add-source PATH_TO_DOWNLOADED_ARTIFACT AudioBookMeta.Tool
dotnet audiobookmeta search --title "Dune" --author "Frank Herbert"
```

`PATH_TO_DOWNLOADED_ARTIFACT` is the directory containing `AudioBookMeta.Tool.*.nupkg`. For a published NuGet package, omit `--add-source PATH_TO_DOWNLOADED_ARTIFACT`.

You can also build a self-contained executable from source:

```sh
git clone https://github.com/JKamsker/AudioBookMeta.Tool.git
cd AudioBookMeta.Tool
dotnet publish src/AudioBookMeta.Tool -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/audiobookmeta
./artifacts/audiobookmeta/dotnet-audiobookmeta --help
```

Replace `linux-x64` with your [.NET runtime identifier](https://learn.microsoft.com/dotnet/core/rid-catalog) when needed, such as `linux-arm64`, `osx-arm64`, or `win-x64`. The resulting executable includes the runtime. The `dotnet-audiobookmeta` executable can be invoked directly; installing the NuGet tool enables the preferred `dotnet audiobookmeta` form.

## Quick start

Run any provider or search command. On first use, `dotnet audiobookmeta` creates a configuration with
the public Libex, AudioSilo, and Lismio providers. Only Libex belongs to the default search group:

```sh
dotnet audiobookmeta providers list
dotnet audiobookmeta search "Project Hail Mary"
```

Inspect or change settings without editing TOML by hand:

```sh
dotnet audiobookmeta config path
dotnet audiobookmeta config get search.limit
dotnet audiobookmeta config set search.limit 20
dotnet audiobookmeta config validate
```

Add a custom provider with dot-separated configuration keys:

```sh
dotnet audiobookmeta config set providers.catalog.type abs
dotnet audiobookmeta config set providers.catalog.base_url https://metadata.example/catalog
dotnet audiobookmeta config set providers.catalog.groups default,books
dotnet audiobookmeta config validate
```

Lismio is available without being contacted by ordinary searches. Request it explicitly, or add it to
the default group if you want every search to use it:

```sh
dotnet audiobookmeta search "Dune" --provider lismio
dotnet audiobookmeta config set groups.default libex,lismio
```

Use secret references such as `env:NAME` or `file:/path` for credentials; do not put secret values directly on a command line where shell history may retain them.

## Search

Free-text searches can be incomplete or slightly misspelled:

```sh
dotnet audiobookmeta search "har pot philos"
dotnet audiobookmeta search "proj hail" --author weir
```

Use structured fields when you know them:

```sh
dotnet audiobookmeta search --title "Dune" --author "Frank Herbert"
dotnet audiobookmeta search --isbn 9780441172719 --exact
dotnet audiobookmeta search --series "The Expanse" --narrator "Jefferson Mays"
dotnet audiobookmeta search --provider libex --sku BK_HOER_002668
dotnet audiobookmeta search --title "Bertrams Hotel" --author "Agatha Christie" \
  --duration 22642s --duration-tolerance 90s --editions --explain
```

By default, related results are grouped into works. Show individual editions and audiobook recordings with:

```sh
dotnet audiobookmeta search "The Hobbit" --editions
```

Useful search options include:

| Option | Use it to |
| --- | --- |
| `-p, --provider ID` | Search only one provider; repeat it for several providers |
| `--group NAME` | Search a configured provider group |
| `--exclude ID` | Skip a provider |
| `--limit N` | Change the number of displayed results |
| `--fresh` | Ignore cached results |
| `--exact` | Disable fuzzy matching |
| `--editions` | Show individual editions and recordings |
| `--sku VALUE`, `--ufid VALUE` | Resolve an Audible SKU natively in Libex and use it as local identifier evidence elsewhere |
| `--duration DURATION` | Rank editions against a known local duration; providers are never asked to filter by it |
| `--duration-tolerance DURATION` | Set the acceptable duration difference (default: `90s`) |
| `--publisher TEXT` | Add publisher evidence to local edition ranking |
| `--shop-links` | Hydrate search cards with shop URLs; potentially much slower |
| `--no-dedupe` | Show every provider result separately |
| `--explain` | Show scoring evidence and provider warnings |
| `--strict` | Return a failure if any selected provider fails |

Run `dotnet audiobookmeta search --help` for every available filter and option.

## Choose providers

See the configured providers and their short names:

```sh
dotnet audiobookmeta providers list
dotnet audiobookmeta providers show libex
dotnet audiobookmeta providers capabilities audiosilo
```

Search only selected providers:

```sh
dotnet audiobookmeta search "Dune" -p libex -p audiosilo -p lismio
dotnet audiobookmeta search "Dune" --group audiobook
dotnet audiobookmeta search "Dune" --group shop-links --shop-links --limit-per-provider 3
dotnet audiobookmeta search "Dune" --provider libex --page 2
```

`--page` selects a native zero-based provider page from 0 to 9. It refuses providers that cannot honor native pagination; Libex and Lismio support it.

Provider names and groups come from your configuration file. See the [configuration guide](docs/configuration.md) to add community or self-hosted Audiobookshelf-compatible providers.

## List an author's Libex audiobooks

Use Libex's dedicated author lookup when you want its complete author result set instead of a locally ranked free-text search:

```sh
dotnet audiobookmeta author books "Andy Weir"
dotnet audiobookmeta author books "Andy Weir" --provider libex --region uk --json
```

When exactly one enabled Libex provider is configured, `--provider` is optional. Multiple enabled Libex instances require an explicit provider ID.

Ordinary Libex search also uses this author endpoint automatically when structured and quick search return no candidates. JSON records expose `lookup_strategy`, and provider statuses expose `lookup_strategies`, so scripts can distinguish `sku`, `structured_search`, and `author_fallback` results. HTTP 404 from a Libex search or fallback is treated as a successful no-result response rather than a connectivity failure.

## Retrieve one result

When a result has a provider record ID, retrieve it directly with `PROVIDER:ID`:

```sh
dotnet audiobookmeta get libex:B08G9PRS1K
dotnet audiobookmeta get libex:B08G9PRS1K --region uk
dotnet audiobookmeta get audiosilo:work/project-hail-mary
dotnet audiobookmeta get lismio:38299
```

Libex ASINs are normalized to uppercase and must contain exactly ten letters or digits. Human output includes authors, narrators, series, duration, release details, rating, availability, regions, links, and a cleaned description when Libex supplies them.

An ordinary Lismio search returns only the catalogue cards from one request. Add `--shop-links` when you
explicitly want each card hydrated with contributor roles, editions, EAN, collections, and direct shop
links for services such as Audible, BookBeat, Deezer, Spotify, and Storytel. Hydration can make one detail
request per candidate, so combine it with a small `--limit-per-provider`. `get` retrieves one complete
record. Human output uses friendly shop names; JSON and JSONL use stable canonical IDs in `shop_links`.

The generated first-run configuration puts only Libex in `default`; Lismio is registered only in
`shop-links`, so regular searches do not contact it. Select it with `--provider lismio` or
`--group shop-links`, or add it to `groups.default`. If no `default_group` is configured, the CLI selects
all enabled providers, including Lismio.

Not every provider supports direct retrieval; generic Audiobookshelf-compatible providers are usually search-only.

## Use in scripts

Human-readable tables are the default. Request machine-readable output explicitly:

```sh
dotnet audiobookmeta search "Dune" --json
dotnet audiobookmeta search "Dune" --jsonl
dotnet audiobookmeta providers list --json
```

`--json` emits a versioned document. `--jsonl` emits one search result per line. Primary data goes to standard output; errors and human diagnostics go to standard error.

See the [automation guide](docs/automation.md) for output stability, exit codes, and non-interactive behavior.

## Troubleshooting

Validate configuration and test provider connectivity first:

```sh
dotnet audiobookmeta config validate
dotnet audiobookmeta providers test --timeout 10s
```

If a search times out, try a longer provider timeout and deadline:

```sh
dotnet audiobookmeta search "Dune" --timeout 10s --deadline 15s
```

Use `--verbose` to show the resolved configuration, providers, and time limits. `--fresh` rules out a stale local cache entry. Error messages include a suggested next step, and unexpected failures may save a redacted diagnostic log next to the configuration file.

## More documentation

- [Configuration and providers](docs/configuration.md)
- [Automation and JSON output](docs/automation.md)
- [Contributor guide and architecture](docs/development.md)
- [Documentation index](docs/README.md)
