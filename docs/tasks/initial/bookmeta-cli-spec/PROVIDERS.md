# Provider catalog

This catalog covers Libex plus every project in the supplied Audiobookshelf community-provider document. `abs-agg` and the Czech provider aggregate multiple underlying sources, so their subproviders are also documented.

## Top-level projects/adapters

| Provider/project | Recommended CLI adapter | Main scope | Notes |
|---|---|---|---|
| Libex | `libex` native | Audible metadata | Rich API: quick search, structured search, ASIN lookup, authors, narrators, series, chapters, DB filters, releases. |
| abs-tract | generic `abs` per endpoint | Goodreads + Kindle | Separate Goodreads and regional Kindle base URLs. |
| lubimyczytac-abs | `abs` | Polish books | LubimyCzytac metadata; detailed book fields and covers. |
| audioteka-abs | `abs` | Audioteka PL/CZ | Audiobook-specific metadata including lectors/narrators. |
| abs-agg | `abs` per subprovider | 14 current subproviders | Path-segment parameters; some hosted instances documented. |
| abs-storytel-provider | `abs` | Storytel | Regional all/book/audiobook endpoints, caching, narrator/duration on audiobooks. |
| abs-opds | `abs` with URL-template care | arbitrary OPDS catalogs | Generic metadata fallback, cover cropping. |
| abs-audioknihi | `abs` | Belarusian AudioKnihi | AI-generated catalog. |
| Abs-Ximalaya | `abs` | Ximalaya | Chinese-language provider; upstream README is sparse on returned fields. |
| abs-ranobedb | `abs` | Japanese light novels | RanobeDB-specific, rate-limited/cache-aware. |
| audiobookshelf_czech_metadata | `abs` | Czech audiobook stores | Aggregates many Czech storefronts and exposes per-source base paths. |
| abs-metadata-podium | `abs` | Podium Entertainment | Early-beta HTML scraper; search-only ABS contract. |
| AudioSilo Meta | `audiosilo` native or `abs` | open community audiobook DB | Work/recording/person/series model, FTS, ASIN/ISBN lookup, chapters, ABS facade. |

## Files

- `providers/libex.md`
- `providers/abs-tract.md`
- `providers/lubimyczytac-abs.md`
- `providers/audioteka-abs.md`
- `providers/abs-agg.md`
- `providers/abs-agg-sources.md`
- `providers/abs-storytel-provider.md`
- `providers/abs-opds.md`
- `providers/abs-audioknihi.md`
- `providers/abs-ximalaya.md`
- `providers/abs-ranobedb.md`
- `providers/abs-czech-metadata.md`
- `providers/abs-metadata-podium.md`
- `providers/audiosilo-meta.md`

## Hosted providers from the supplied Audiobookshelf community file

The supplied documentation listed community-hosted endpoints for selected `abs-agg` sources, a bandwidth-limited AudioKnihi endpoint, and AudioSilo Meta. These are third-party/community services and may change or disappear; configuration should treat hosted URLs as user-editable defaults, not constants compiled into code.

The same supplied documentation explicitly warns that community providers are not maintained or security-reviewed by the Audiobookshelf team. `bookmeta` should therefore avoid treating any community endpoint as implicitly trusted.
