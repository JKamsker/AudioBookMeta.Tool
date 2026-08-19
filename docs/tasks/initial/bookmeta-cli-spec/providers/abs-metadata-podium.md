# abs-metadata-podium

Repository: `https://github.com/lkiesow/abs-metadata-podium`

Recommended adapter: generic `abs`.

## Scope

Podium Entertainment metadata by searching and scraping public Podium web pages.

## API

Implements the standard single `GET /search` custom-provider endpoint.

## Deployment

Reviewed runtime options include:

- `ABS_METADATA_PODIUM_TOKEN` — optional shared secret expected in `AUTHORIZATION`;
- `ABS_METADATA_PODIUM_HOST` — default `0.0.0.0`;
- `ABS_METADATA_PODIUM_PORT` — default `8000`;
- `ABS_METADATA_PODIUM_DEBUG` — debug logging.

ABS base is the server root; token should match if enabled.

## Reliability warning

Upstream labels this early beta and states it scrapes HTML, has no cache/database/retry logic, and may break when Podium changes its site. The CLI should use short timeouts, ordinary retry policy only where safe, and surface parsing/server errors without failing other providers.
