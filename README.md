# AudioBookMeta.Tool

Search book and audiobook metadata from several providers with one command.

`dotnet audiobookmeta` is useful when you know only part of a title, want to compare results from different sources, or need a stable JSON result for a script. It ranks imperfect matches locally, keeps different audiobook recordings separate, and continues when one provider is temporarily unavailable.

It is read-only: it does not edit your Audiobookshelf library, media files, or provider data.

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

First, ask `dotnet audiobookmeta` where it expects its configuration file:

```sh
dotnet audiobookmeta config path
```

Create that file with three public metadata providers:

```toml
version = 1
default_group = "default"

[search]
deadline = "20s"

[providers.libex]
type = "libex"
base_url = "https://libexdb.com"
enabled = true
region = "us"

[providers.audiosilo]
type = "audiosilo"
base_url = "https://meta.audiosilo.app"
enabled = true

[providers.lismio]
type = "lismio"
base_url = "https://lismio.app"
enabled = true
region = "de"
timeout = "15s"

[groups]
default = ["libex", "audiosilo", "lismio"]
```

Check the file, then try a search:

```sh
dotnet audiobookmeta config validate
dotnet audiobookmeta providers test --timeout 10s
dotnet audiobookmeta search "Project Hail Mary"
```

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

## Retrieve one result

When a result has a provider record ID, retrieve it directly with `PROVIDER:ID`:

```sh
dotnet audiobookmeta get libex:B08G9PRS1K
dotnet audiobookmeta get libex:B08G9PRS1K --region uk
dotnet audiobookmeta get audiosilo:work/project-hail-mary
dotnet audiobookmeta get lismio:38299
```

Libex ASINs are normalized to uppercase and must contain exactly ten letters or digits. Human output includes authors, narrators, series, duration, release details, rating, availability, regions, links, and a cleaned description when Libex supplies them.

Lismio searches hydrate each result with its detail page. That adds contributor roles, editions, EAN,
collections, and direct shop links for services such as Audible, BookBeat, Deezer, Spotify, Storytel,
and other shops catalogued by Lismio. Human search output lists the available shop names; `get` prints
the URLs, and `--json` includes them in `shop_links` for both search and direct retrieval.

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
