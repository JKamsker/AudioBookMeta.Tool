# abs-agg

Repository: `https://github.com/Vito0912/abs-agg`

Recommended adapter: generic `abs` for each configured subprovider. A convenience `abs-agg` adapter MAY help generate base URLs and validate path parameters, but is not required.

## Model

`abs-agg` is one server that exposes many metadata sources through a consistent route:

```text
GET /:provider/search?title=<title>&author=<author>
```

Provider options are encoded as path segments before `/search`:

```text
/provider/<parameter1>/<parameter2>/search?title=...&author=...
```

Audiobookshelf configuration uses the same base without the final `/search`.

## Current provider catalog in reviewed repository documentation

The repository's `Providers.md` listed 14 providers at review time:

1. ARD Audiothek
2. Audioteka
3. Big Finish
4. BookBeat
5. Deezer
6. Die drei ???
7. Goodreads
8. Graphic Audio
9. Hardcover
10. LibriVox
11. Libro.fm
12. Soundbooth Theater
13. Storytel
14. The StoryGraph

See `abs-agg-sources.md` for per-source fields and parameters.

## Server configuration

Reviewed README documented:

- default port `3000`;
- optional `HARDCOVER_TOKEN`;
- optional `GOODREADS_KEY`;
- optional `DEEZER_ACCESS_TOKEN` for higher rate limits.

## Hosted-provider behavior

The supplied Audiobookshelf community file listed selected hosted `abs-agg` endpoints under `provider.vito0912.de` and auth value `abs`. The provider's own documentation warns hosted endpoints can break and can be replaced by self-hosted deployment.

The CLI MUST therefore model each hosted endpoint as an ordinary configurable instance, not as a permanent guaranteed service.
