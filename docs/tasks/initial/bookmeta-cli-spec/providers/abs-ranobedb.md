# abs-ranobedb

Repository: `https://github.com/kennethsible/abs-ranobedb`

Recommended adapter: generic `abs`.

## Scope

RanobeDB metadata for Japanese light novels and official translations, designed to improve handling of volume/series data compared with generic book sources.

## Deployment/configuration

Reviewed Docker example uses port `5000` and documents:

- `LOG_LEVEL=INFO`;
- `SEARCH_LIMIT=5`;
- `PREFER_ROMAJI=true`;
- `AMAZON_COVERS=false`;
- persistent cache volume.

The provider has an internal documented rate limit of 60 requests/minute and caches redundant API calls.

ABS configuration documents no authorization header value.

## CLI implications

- Good candidate for a `light-novel` provider group.
- Preserve original/romaji titles exactly as returned; use normalization only for ranking.
- `PREFER_ROMAJI` is deployment behavior, not a per-query CLI switch unless the upstream provider later exposes it dynamically.
