# abs-opds

Repository: `https://github.com/DeXP/abs-opds`

Recommended adapter: generic `abs` through the provider facade, with explicit URL-template support.

## Scope

Turns arbitrary OPDS book catalogs into an Audiobookshelf custom metadata source. Upstream notes OPDS is commonly ebook-oriented and is useful as a metadata fallback for covers/descriptions even when audio-specific data is absent.

## Documented fields

- cover;
- title;
- author;
- genres;
- description.

## Configuration

The provider URL embeds the target OPDS catalog, for example conceptually:

```text
.../abs-opds.php?opds=<OPDS_URL>&do=
```

Docker can serve the facade at the exposed port root.

## Cover processing

Optional `crop` values: top, bottom, left, right, center. Optional `skip` 0–100 controls percentage skipped from a crop side (ignored for center).

## CLI implications

- Treat each OPDS catalog + crop configuration as a separate provider instance if users want independent selection.
- Search semantics depend on the underlying OPDS catalog; incomplete matching is unknown.
- The generic ABS adapter may need a configurable request-template mode because this provider's query layout is unusual.
