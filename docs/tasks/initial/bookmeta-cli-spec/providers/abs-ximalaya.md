# Abs-Ximalaya

Repository: `https://github.com/shanyan-wcx/Abs-Ximalaya`

Recommended adapter: generic `abs`.

## Scope

Audiobookshelf metadata provider for Ximalaya (喜马拉雅).

## Deployment details documented

- Docker image: `shanyanwcx/abs-ximalaya:latest`.
- Default port: `7814`.
- `PORT` environment variable can change the port.
- `TZ` can set timezone.
- ABS should be configured with `http://IP:PORT` and no trailing `/`.
- The repository credits `qilishidai/ximalaya-API` for the Ximalaya API implementation.

## Capability notes

The reviewed README does not enumerate returned metadata fields or precise fuzzy-search behavior. Keep those capabilities `unknown` rather than assuming them from other ABS providers.
