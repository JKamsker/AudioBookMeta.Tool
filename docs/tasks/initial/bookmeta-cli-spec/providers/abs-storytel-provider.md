# abs-storytel-provider

Repository: `https://github.com/Revisor01/abs-storytel-provider`

Recommended adapter: generic `abs`.

## Features documented

- high-resolution 1200x1200 covers;
- title/series cleanup and subtitle extraction;
- 20+ language/region support;
- separate all-media, ebook, and audiobook endpoints;
- audiobook narrator, duration, publisher, ISBN;
- author relevance ranking;
- retry without author when combined search finds no results;
- multi-series support;
- tags;
- description HTML cleanup;
- abridged/unabridged marker removal in many languages;
- persistent SQLite search cache;
- configurable result limit 1–50, default 20.

## Base paths

For region `<region>`:

- `/<region>` — all media.
- `/<region>/book` — ebooks.
- `/<region>/audiobook` — audiobooks.

Search endpoint under those bases is `/search?query=...` with optional `author` and `limit`.

## Region examples documented

`ae`, `bg`, `br`, `dk`, `nl`, `en`, `fi`, `fr`, `de`, `il`, `is`, `id`, `it`, `no`, `pl`, `ru`, `es`, `se`, `tr`, `in`, with the note that other valid Storytel region codes can work.

## Authentication

Optional. If server env `AUTH` is set, requests must carry exactly that value in the `Authorization` header.

## Caching caveat

The repository describes a persistent cache with no expiration. `bookmeta --fresh` cannot guarantee fresh Storytel data through this provider unless the upstream adds a cache-bypass mechanism; the CLI should report that limitation in explain mode.
