# AudioSilo Meta

Repository: `https://github.com/KodeStar/audiosilo-meta`

Recommended adapter: native `audiosilo`; generic `abs` is also supported through its ABS facade.

## Scope/data model

Open community audiobook database with first-class:

- Work — abstract book;
- Recording — specific narration with narrators, runtime, release data, publisher, region-scoped ASINs/ISBNs, cover, chapters;
- Person — shared author/narrator entity;
- Series — ordered work membership.

This model maps well to the CLI's work-versus-recording clustering distinction.

## Native API

Reviewed README documents read-only API endpoints under `/api/v1`, including:

- `search?q=&limit=`;
- `works/latest`;
- work detail;
- recording chapter lists;
- person detail;
- series detail;
- `lookup?asin=|isbn=`;
- stats/coverage endpoints.

All public data API access is documented as unauthenticated.

## Search behavior

Native full-text search matches whole words and word prefixes rather than arbitrary mid-word substrings. This supports partial token input such as a word prefix but does not eliminate the need for CLI fuzzy ranking.

## ABS facade

Hosted ABS base documented by upstream and in the supplied community file:

```text
https://meta.audiosilo.app/abs
```

No auth.

The ABS endpoint is `GET /abs/search`. Upstream documents `query` with optional `author` and `isbn`, returns up to 10 matches, and emits one entry per recording.

Documented returned fields through the ABS facade include title, subtitle, author, narrator, publisher, publishedYear, description, cover, ISBN, ASIN, series+sequence, language, duration, and genres. Tags are deliberately not returned.

## Native capability mapping

| Capability | Status |
|---|---|
| free-text search | yes |
| prefix search | yes |
| ASIN lookup | yes |
| ISBN lookup | yes |
| work detail | yes |
| recording detail | via work recording model |
| chapters | yes |
| person/series detail | yes |
| authentication | no for public data API |

## Licensing note

Upstream documents factual core data as CC0, derived characters/recaps as CC BY-SA, and code as AGPL-3.0-only. The CLI should preserve attribution/license metadata if it ever begins exporting derived fields beyond the ABS facade.
