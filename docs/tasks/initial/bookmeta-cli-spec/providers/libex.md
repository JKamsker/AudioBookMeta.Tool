# Libex

## Role

Libex is a rich, unrestricted Audible metadata API. The supplied OpenAPI file identifies version `1.15.0` and the public server `https://libexdb.com`.

Recommended adapter: native `libex`.

## Why native instead of only ABS compatibility

Libex exposes much more than the ABS search facade: quick suggestions, structured search, direct ASIN lookup, bulk lookup, chapters, author/narrator/series APIs, local-DB filtering, categories, and release windows.

## Search endpoints

### `/quick-search`

Required `keywords`, optional `region`. Documented as Audible suggestions. This is the preferred first request for incomplete free-text input.

### `/search`

Optional search parameters include:

- `title`
- `author`
- `narrator`
- `publisher`
- `keywords`
- `query`
- `products_sort_by`
- `limit` (1–50, default 10)
- `page` (0–9)
- `cache`
- `region` (CLI default `de`; Libex itself defaults to `us` when omitted)

### ABS compatibility

- `/{region}/search`
- `/{region}/quick-search/search`

These return `{"matches": [...]}` in an Audiobookshelf-oriented shape.

## Retrieval endpoints

- `/book/{asin}` — single book by ASIN.
- `/book` — bulk ASIN lookup, up to 1000 ASIN inputs according to the supplied spec.
- `/book/sku/{sku}` — SKU-group variants from the local DB/cache behavior described by the API.
- `/book/{asin}/chapters` — chapter data.

## People and series

- `/author?name=`
- `/author/{asin}`
- `/author/books?name=`
- `/author/books/{asin}`
- `/narrator/books?name=`
- `/series/search?name=`
- `/series/{asin}`
- `/series/books/{asin}`

## Releases/categories

- `/new-releases`
- `/coming-soon`
- `/categories`

Release windows are constrained in the supplied schema to 30, 60, 90, 120, 240, or 365 days.

## Local database API

The `/db/*` endpoints query the indexed local dataset rather than only live Audible search. They include book filters, plans, genres, virtual-voice books, releases, author/narrator/series relations, chapters, and stats.

`/db/book` supports extensive filters such as title, subtitle, region, description, publisher, copyright, ISBN, author/series name, language, rating range, duration range, explicit, Whispersync, PDF, book/content type, listenability, buyability, virtual voice, plan name, genre partial match, and exact category IDs.

## Book response fields

The supplied `BookResponse` schema includes, among others:

- ASIN, title, subtitle, description, summary;
- region/regions;
- publisher, copyright, ISBN, language;
- rating, format, release date;
- explicit/PDF/Whispersync flags;
- image URL, duration minutes, link;
- content type/delivery type;
- episode data;
- SKU/SKU group;
- availability/listenability/buyability/VVAB;
- plans;
- authors, narrators, genres, series.

## CLI capability mapping

| Capability | Status |
|---|---|
| free text | yes |
| incomplete/quick search | yes |
| title/author/narrator/publisher filters | yes |
| ASIN lookup | yes |
| ISBN search | via DB filter; native live `/search` does not document ISBN parameter |
| region | yes |
| get by ID | yes |
| bulk get | yes |
| chapters | yes |
| author/series search | yes |
| rich filters | yes |
| health | yes (`/health`) |

## Proposed search plan

1. If ASIN supplied: direct book lookup.
2. If free-text is short/incomplete: quick-search.
3. If structured hints exist or quick-search is insufficient: `/search`.
4. Normalize all candidates and local-rank.
5. Use `/db/book` only when the user asks for indexed/local-DB filtering, not silently as a substitute for live search.
