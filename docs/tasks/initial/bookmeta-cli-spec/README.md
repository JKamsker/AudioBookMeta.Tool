# bookmeta CLI specification bundle

This bundle specifies a provider-agnostic command-line tool for finding book and audiobook metadata across multiple metadata providers, including Audiobookshelf-compatible custom providers and richer native APIs such as Libex and AudioSilo Meta.

The core design principle is:

> One normalized search request → concurrent provider adapters → normalized candidates → local ranking and work/edition clustering → human or machine-readable output.

## Bundle contents

- `SPEC.md` — normative CLI/product specification.
- `PROVIDER-CONTRACT.md` — adapter interface, capabilities, error model, and provider lifecycle.
- `SEARCH-RANKING.md` — incomplete-query behavior, normalization, ranking, and deduplication/clustering.
- `CONFIG.md` — configuration format, provider selection, secrets, groups, and examples.
- `OUTPUT-SCHEMA.md` — normalized result and JSON-output conventions.
- `IMPLEMENTATION.md` — architecture, concurrency, caching, retries, security, and implementation phases.
- `TEST-PLAN.md` — unit, contract, integration, and acceptance tests.
- `PROVIDERS.md` — provider matrix and links to per-provider notes.
- `providers/` — detailed notes for Libex and every provider/project listed in the supplied Audiobookshelf community-provider document, plus current `abs-agg` subproviders.
- `schemas/` — JSON Schemas for normalized requests, results, and provider configuration.
- `examples/` — example configuration and JSON output.
- `sources/` — the two source files supplied with this request, plus a research/source index.

## Status language

Provider notes distinguish between:

- **Documented** — stated in the supplied source files or provider repository documentation checked on 2026-08-19.
- **Derived** — a straightforward implementation consequence of a documented API shape.
- **Proposed** — behavior defined by this CLI specification, not claimed to exist in the upstream provider.
- **Unknown** — upstream documentation reviewed for this bundle did not establish the behavior.

## Scope

The CLI is designed to support arbitrary future providers. The provider catalog in this bundle is a seed registry, not a hardcoded closed list.
