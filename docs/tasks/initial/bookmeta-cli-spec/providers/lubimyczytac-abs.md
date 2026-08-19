# lubimyczytac-abs

Repository: `https://github.com/lakafior/lubimyczytac-abs`

Recommended adapter: generic `abs`.

## Source/scope

Metadata from LubimyCzytac, a major Polish book site.

## Documented returned/fetched metadata

- cover;
- title;
- author;
- description with HTML removed;
- publisher;
- publish year;
- series and series number;
- genres;
- tags;
- language;
- ISBN;
- rectangular audiobook covers where the item exposes an audiobook type.

The repository also documents search/matching improvements such as alternate cover sources, an ISBN-related similarity penalty, handling HTTP 429, and optional fake ISBN `0` for sufficiently similar items without ISBN.

## Deployment

Docker image: `lakafior/lubimyczytac-abs:latest`, documented port `3000`.

The README instructs Audiobookshelf users to configure the base URL without trailing slash and to provide a nonblank authorization value even though it is example-like rather than described as a secret.

## CLI notes

- Treat it as a standard ABS search provider.
- Do not copy its fake-ISBN behavior into cross-provider normalization. An ISBN value of `0` should be treated as non-identifier/sentinel if encountered.
- Rate-limit/429 handling should honor upstream responses and CLI retry policy.
- Polish text normalization must be Unicode-safe and must not strip diacritics from displayed values.
