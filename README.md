# bookmeta

Search book and audiobook metadata from several providers with one command.

`bookmeta` is useful when you know only part of a title, want to compare results from different sources, or need a stable JSON result for a script. It ranks imperfect matches locally, keeps different audiobook recordings separate, and continues when one provider is temporarily unavailable.

It is read-only: it does not edit your Audiobookshelf library, media files, or provider data.

## Install

`bookmeta` currently builds from source and requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```sh
git clone https://github.com/JKamsker/bookmeta-cli.git
cd bookmeta-cli
dotnet publish src/BookMeta.Cli -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/bookmeta
./artifacts/bookmeta/bookmeta --help
```

Replace `linux-x64` with your [.NET runtime identifier](https://learn.microsoft.com/dotnet/core/rid-catalog) when needed, such as `linux-arm64`, `osx-arm64`, or `win-x64`. The resulting executable includes the runtime. Copy `bookmeta` (`bookmeta.exe` on Windows) to a directory on your `PATH` to use it from anywhere.

## Quick start

First, ask `bookmeta` where it expects its configuration file:

```sh
bookmeta config path
```

Create that file with two public metadata providers:

```toml
version = 1
default_group = "default"

[providers.libex]
type = "libex"
base_url = "https://libexdb.com"
enabled = true
region = "us"

[providers.audiosilo]
type = "audiosilo"
base_url = "https://meta.audiosilo.app"
enabled = true

[groups]
default = ["libex", "audiosilo"]
```

Check the file, then try a search:

```sh
bookmeta config validate
bookmeta providers test --timeout 10s
bookmeta search "Project Hail Mary"
```

## Search

Free-text searches can be incomplete or slightly misspelled:

```sh
bookmeta search "har pot philos"
bookmeta search "proj hail" --author weir
```

Use structured fields when you know them:

```sh
bookmeta search --title "Dune" --author "Frank Herbert"
bookmeta search --isbn 9780441172719 --exact
bookmeta search --series "The Expanse" --narrator "Jefferson Mays"
```

By default, related results are grouped into works. Show individual editions and audiobook recordings with:

```sh
bookmeta search "The Hobbit" --editions
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

Run `bookmeta search --help` for every available filter and option.

## Choose providers

See the configured providers and their short names:

```sh
bookmeta providers list
bookmeta providers show libex
bookmeta providers capabilities audiosilo
```

Search only selected providers:

```sh
bookmeta search "Dune" -p libex -p audiosilo
bookmeta search "Dune" --group audiobook
```

Provider names and groups come from your configuration file. See the [configuration guide](docs/configuration.md) to add community or self-hosted Audiobookshelf-compatible providers.

## Retrieve one result

When a result has a provider record ID, retrieve it directly with `PROVIDER:ID`:

```sh
bookmeta get libex:B08G9PRS1K
bookmeta get audiosilo:work/project-hail-mary
```

Not every provider supports direct retrieval; generic Audiobookshelf-compatible providers are usually search-only.

## Use in scripts

Human-readable tables are the default. Request machine-readable output explicitly:

```sh
bookmeta search "Dune" --json
bookmeta search "Dune" --jsonl
bookmeta providers list --json
```

`--json` emits a versioned document. `--jsonl` emits one search result per line. Primary data goes to standard output; errors and human diagnostics go to standard error.

See the [automation guide](docs/automation.md) for output stability, exit codes, and non-interactive behavior.

## Troubleshooting

Validate configuration and test provider connectivity first:

```sh
bookmeta config validate
bookmeta providers test --timeout 10s
```

If a search times out, try a longer provider timeout and deadline:

```sh
bookmeta search "Dune" --timeout 10s --deadline 15s
```

Use `--verbose` to show the resolved configuration, providers, and time limits. `--fresh` rules out a stale local cache entry. Error messages include a suggested next step, and unexpected failures may save a redacted diagnostic log next to the configuration file.

## More documentation

- [Configuration and providers](docs/configuration.md)
- [Automation and JSON output](docs/automation.md)
- [Contributor guide and architecture](docs/development.md)
- [Documentation index](docs/README.md)
