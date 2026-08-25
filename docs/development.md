# Development and architecture

This page is for contributors and provider-adapter maintainers. User installation and search examples live in the repository [README](../README.md).

## CLI product contract

`dotnet audiobookmeta` is a **multi-surface service CLI**: one search can contact several independently configured provider targets, each with its own URL, credentials, timeouts, and capabilities.

The task-first command tree is:

```text
dotnet audiobookmeta
├── search [QUERY]
├── get PROVIDER:ID
├── author
│   └── books NAME
├── providers
│   ├── list
│   ├── show PROVIDER
│   ├── test [PROVIDER...]
│   └── capabilities [PROVIDER...]
├── config
│   ├── path
│   ├── get KEY
│   ├── set KEY VALUE
│   ├── unset KEY
│   └── validate
└── completion SHELL
```

Search and direct retrieval stay at the top level. Provider-native author lookup has a domain branch, while provider discovery and local configuration management are grouped into secondary operational branches.

A provider ID is the explicit target selector. The normalized full base URL, including a path prefix, is the target and credential identity boundary because ABS-compatible deployments may use path-scoped providers. Credentials are provider-local `env:`, `file:`, or `literal:` references; there is no implicit profile, account, fallback host, or interactive authentication flow.

Resolution is deterministic:

1. Command flags override loaded settings.
2. `--config` overrides `BOOKMETA_CONFIG`, which overrides the platform path.
3. TOML values override built-in defaults.
4. Explicit providers/groups override the default group; otherwise all enabled providers are used.
5. Exclusions are applied after group expansion.

The automation, non-interactive, destructive-action, stream-routing, diagnostic, and exit-code policies are documented in [automation.md](automation.md).

## Build and test

The repository pins .NET SDK 10 in `global.json`.

Configure the repository hook once per clone so oversized handwritten C# files are caught before commit:

```sh
git config core.hooksPath .githooks
```

```sh
pwsh -NoProfile -File eng/check-source-length.ps1
dotnet restore
dotnet build AudioBookMeta.Tool.slnx
dotnet test AudioBookMeta.Tool.slnx
dotnet format AudioBookMeta.Tool.slnx --verify-no-changes
dotnet pack src/AudioBookMeta.Tool -c Release --no-build --no-restore -o artifacts/nuget
```

The source-length check warns above 300 lines and rejects handwritten C# files above 500 lines. A hard-limit exception requires a path and non-empty justification in `eng/source-length-exceptions.txt`; generated, build-output, and intermediate files are excluded.

GitHub Actions repeats the source-length, formatting, Release build, and test checks. It then packs `AudioBookMeta.Tool` as a NuGet tool, installs the package into an isolated tool directory, verifies the `dotnet audiobookmeta` command, and uploads the `.nupkg` as the run's `audiobookmeta-tool-nuget-*` artifact.

Publish a framework-dependent build for local inspection with:

```sh
dotnet publish src/AudioBookMeta.Tool -c Release -o artifacts/audiobookmeta
dotnet artifacts/audiobookmeta/dotnet-audiobookmeta.dll --help
```

The user-facing self-contained publish command is documented in the repository [README](../README.md).

## Source layout

```text
src/AudioBookMeta.Tool/
├── Commands/          Spectre.Console.Cli commands and settings
├── Common/            process-wide output, errors, DI, and diagnostics
├── Configuration/     TOML loading, validation, selection, and secrets
├── Generated/         Kiota-generated Libex and AudioSilo clients
├── Model/             normalized request, result, and provider models
├── Providers/         ABS, Libex, AudioSilo, and Lismio adapters
├── Render/            human and JSONL search rendering
└── Search/            concurrency, cache, ranking, and clustering

tests/AudioBookMeta.Tool.Tests/
```

`Program.cs` owns the visible command tree and dependency registration. Each non-generated command file contains one command class plus its settings type. Provider I/O stays behind adapter and transport abstractions rather than inside command handlers.

## Search pipeline

```text
CLI request
  → configuration and provider selection
  → concurrent provider execution
  → provider-specific normalization
  → local ranking
  → work and edition clustering
  → human, JSON, or JSONL rendering
```

Each provider receives a per-provider timeout and the shared search deadline. Global provider concurrency defaults to eight. Adapters that expand multiple internal requests use their own smaller bound. Idempotent transient failures receive at most one retry.

Normalized successful searches are cached on disk. The key includes provider ID/type/base URL, normalized request, and edition mode. `--fresh` and `--raw` bypass reads; raw responses and failures are not written to the normalized cache.

## Provider adapters

### Generic ABS

The hand-written ABS adapter targets the minimal Audiobookshelf custom-provider `GET /search` contract. There is no single OpenAPI description shared by community implementations, so parsing is defensive and only `title` is required. Response depth, result count, raw size, redirects, and HTTP security boundaries are constrained.

### Libex and AudioSilo

Libex and AudioSilo publish OpenAPI descriptions. Their normal search, lookup, detail, and health paths use typed clients generated by Kiota. Libex's author-books endpoint uses the same bounded transport because it is outside the repository's currently generated endpoint set. Hand-written mappers translate wire models into `SearchResult` without leaking provider model shapes into the public contract.

`--raw` deliberately uses the bounded transport and `JsonDocument` path so the exact provider payload can be attached rather than reconstructed from generated models.

### Lismio

Lismio exposes public HTML catalogue pages rather than an OpenAPI contract. The adapter uses bounded
HTML transport and AngleSharp parsers derived from the companion `lismio-api` implementation. Default
search maps catalogue cards with one request. `--shop-links` opts into at most four concurrent detail
requests so results include shop URLs and complete audiobook metadata. A failed detail request retains
the lightweight card as a candidate and adds a warning; malformed direct-detail responses fail as
provider errors. The hydration flag is part of the cache key, so card-only and enriched results cannot
collide.

## Regenerate Kiota clients

Kiota is pinned as a repository-local .NET tool in `.config/dotnet-tools.json`. The generation script restores that tool automatically.

```sh
./eng/generate-clients.sh
dotnet build AudioBookMeta.Tool.slnx
dotnet test AudioBookMeta.Tool.slnx
```

Generation inputs:

- Libex uses the bundled `docs/tasks/initial/bookmeta-cli-spec/sources/libexdb-openapi.json` for reproducibility.
- AudioSilo uses `https://meta.audiosilo.app/api/v1/openapi.json`.

Only the endpoints used by v1 are included. Generated code and each `kiota-lock.json` are committed under `src/AudioBookMeta.Tool/Generated`. Do not hand-edit generated sources; adjust `eng/generate-clients.sh`, regenerate, and commit the resulting diff.

If upstream generation changes unexpectedly, compare the OpenAPI revision, Kiota tool version, included endpoint set, generated lock files, and package/runtime compatibility before changing hand-written adapters.

## Output schemas

The normalized v1 schemas live in `docs/tasks/initial/bookmeta-cli-spec/schemas/` and are versioned independently from providers. Adapter changes should preserve those schemas, including explicit null/empty representation and omission of `raw` unless requested.

When changing output:

1. Add or update normalization and serializer tests.
2. Validate a real or fixture-backed response against the schemas.
3. Verify human list output remains a table rather than JSON.
4. Verify `--json` remains opt-in and carries `schema_version: 1`.
5. Verify JSONL keeps one parseable result per stdout line.

Breaking reinterpretations require a new schema version rather than an unannounced v1 change.

## Test coverage

The suite covers:

- text normalization, ISBN handling, ranking, and exact matching;
- work/edition clustering and conflicting recording identifiers;
- configuration, group recursion, secret resolution, and HTTP safety;
- ABS and Lismio HTML fixtures and typed Kiota fixtures for Libex and AudioSilo;
- concurrency, cancellation, deadlines, strict partial failures, and caching behavior;
- redirect credential safety, oversized responses, terminal escaping, and log redaction;
- help, human list output, JSON output, option validation, and exit codes.

Before committing, run the Release test suite and `git diff --check` in addition to the commands above.

## Specification sources

The original implementation bundle in `docs/tasks/initial/bookmeta-cli-spec/` contains the normative command, configuration, provider, ranking, output, and test requirements. Its `MANIFEST.sha256` protects the research inputs and schemas; do not alter those files casually when updating user or contributor documentation.
