# abs-agg subproviders

These details summarize the `abs-agg` repository's `Providers.md` as reviewed on 2026-08-19. They are upstream capabilities, not guarantees that every hosted deployment is enabled or reachable.

## ARD Audiothek (`ardaudiothek`)

Scope: German public-broadcaster audio/audiobooks.

Optional parameters:

- `limit`: 1–20, default 5.
- `searchType`: `search` or `programsets`.

Returned fields: title, author, description, cover, publisher, genres, tags, series, language.

Comment: German-language-only. Hosted base documented by the repository: `https://provider.vito0912.de/ardaudiothek`, auth `abs`.

## Audioteka (`audioteka`)

Required path parameter:

- `lang`: `pl`, `cz`, `de`, `sk`, `lt`.

Optional `limit`: 1–20, default 5.

Returned: title, author, narrator, description, cover, publisher, genres, tags, series, language, duration.

Example base: `/audioteka/lang:pl`.

## Big Finish (`bigfinish`)

Optional `limit`: 1–5, default 3.

Returned: title, author, narrator, description, cover, ISBN, series, language, publishedYear, publisher, duration.

Good fit for audio-drama metadata.

## BookBeat (`bookbeat`)

Required `market` path parameter. Reviewed docs list markets including Austria, Belgium, Bulgaria, Croatia, Cyprus, Czechia, Denmark, Estonia, Finland, France, Germany, Greece, Hungary, Ireland, Italy, Latvia, Lithuania, Luxembourg, Malta, Netherlands, Norway, Poland, Portugal, Romania, Slovakia, Slovenia, Spain, Sweden, Switzerland, and United Kingdom.

Optional:

- `includeErotic`: true/false, default false.
- `includeHighResCovers`: true/false, default false.

Returned: title, author, description, cover, ISBN, series, language, publishedYear.

Repository notes that searches can involve multiple upstream requests and recommends considering rate limiting for self-hosting.

## Deezer (`deezer`)

Optional `limit`: 1–10, default 5.

Returned: title, author, cover, publisher, publishedYear, genres, duration.

Optional server environment token for higher rate limits: `DEEZER_ACCESS_TOKEN`.

This is music-catalog metadata and may be useful for audio content but should be lower priority for ordinary books unless the user explicitly enables it.

## Die drei ??? (`dreifragezeichen`)

Optional `limit`: 1–5, default 5.

Returned: title, author, narrator, description, cover, publishedYear, series, duration, language, tags.

Focused on the `Die drei ???` universe, including main series and some specials/kids series according to upstream docs.

## Goodreads (`goodreads`)

Optional `limit`: 1–20, default 10.

Returned: title, subtitle, author, description, cover, ISBN, publisher, publishedYear, language, series, genres.

Upstream notes covers can be low quality/missing and the API is deprecated. Hosted base documented with auth `abs`.

## Graphic Audio (`graphicaudio`)

Optional `limit`: 1–20, default 10.

Returned: title, subtitle, author, narrator, description, cover, ISBN, ASIN, genres, series, publishedYear.

Focused on dramatized audiobooks/full-cast productions.

## Hardcover (`hardcover`)

Optional:

- `language`: language code.
- `limit`: 1–25, default 10.

Returned: title, subtitle, author, narrator, description, cover, ISBN, ASIN, publisher, publishedYear, language, series, tags.

Upstream documentation warns search may fail to find some records known to exist. Local cross-provider search is valuable here.

## LibriVox (`librivox`)

Optional:

- `genre`: string.
- `limit`: 1–20, default 10.

Returned: title, author, description, cover, genres, language, duration, publishedYear.

Upstream notes a `^` prefix can anchor title/author search to the beginning of a term. The CLI should not automatically insert that unless exact/prefix behavior is explicitly requested.

## Libro.fm (`librofm`)

Optional:

- `limit`: 1–6.
- `searchby`: `all`, `titles`, `authors`.
- `lang`: large documented language-code enum, default all.
- `ai_narrated`: true/false, default false.

Returned: title, subtitle, author, narrator, publisher, publishedYear, description, cover, ISBN, bookId, genres, series, language, duration.

The upstream docs say it uses an unauthenticated public Libro.fm API.

## Soundbooth Theater (`soundbooththeater`)

No provider parameters documented.

Returned: title, series, cover, author, narrator, description, duration, publishedYear, genres, subtitle.

Upstream marks the implementation highly experimental and potentially unreliable. The CLI should surface that as a provider warning/low default priority rather than suppress results.

## Storytel (`storytel`)

Required `language` path parameter. Reviewed docs list: `en`, `sv`, `no`, `dk`, `fi`, `is`, `de`, `es`, `fr`, `it`, `pl`, `nl`, `pt`, `bg`, `tr`, `ru`, `ar`, `hi`, `id`, `th`.

Optional:

- `limit`: 1–10, default 3.
- `type`: `audiobook`, `ebook`, `all` (default all).

Returned: title, subtitle, author, narrator, description, cover, ISBN, series, language, publishedYear, publisher, duration, tags.

## The StoryGraph (`storygraph`)

Optional `limit`: 1–5, default 3.

Returned: title, author, publisher, publishedYear, description, cover, ISBN, language, and a `poweredBy` field in upstream docs.
