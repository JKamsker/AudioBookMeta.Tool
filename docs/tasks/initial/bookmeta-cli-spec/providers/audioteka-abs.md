# audioteka-abs

Repository: `https://github.com/lakafior/audioteka-abs`

Recommended adapter: generic `abs`.

## Scope

Audioteka audiobook metadata. The reviewed README documents Polish and Czech sites.

## Metadata fields documented

- cover;
- title;
- author;
- publisher;
- series;
- genres;
- language;
- description;
- lectors/narrators;
- audiobook cover.

## Runtime settings documented

- `LANGUAGE=pl` by default, `cz` for Czech;
- `ADD_AUDIOTEKA_LINK_TO_DESCRIPTION=true` by default;
- `METADATA_CONCURRENCY=5` default for detail-page enrichment.

Docker port is documented as `3001`.

The README asks for a nonblank ABS Authorization Header Value. Treat this as deployment-specific configuration rather than a universal credential.

## CLI notes

Configure separate provider instances if the user runs multiple language deployments. Local ranking should use narrator and series evidence when returned.
