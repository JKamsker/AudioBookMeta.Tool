# Source index

## User-supplied files included verbatim

- `community-providers.md` — Audiobookshelf community metadata-provider documentation supplied with the request.
- `libexdb-openapi.json` — Libex OpenAPI document supplied with the request; version in that file is 1.15.0.

## External primary sources reviewed for provider details

Reviewed on 2026-08-19. URLs are listed for traceability; the bundle contains summaries rather than copies of third-party README files.

- Audiobookshelf custom metadata provider specification: https://github.com/advplyr/audiobookshelf/blob/master/custom-metadata-provider-specification.yaml
- abs-tract: https://github.com/ahobsonsayers/abs-tract
- lubimyczytac-abs: https://github.com/lakafior/lubimyczytac-abs
- audioteka-abs: https://github.com/lakafior/audioteka-abs
- abs-agg: https://github.com/Vito0912/abs-agg
- abs-agg provider details: https://github.com/Vito0912/abs-agg/blob/main/Providers.md
- abs-storytel-provider: https://github.com/Revisor01/abs-storytel-provider
- abs-opds: https://github.com/DeXP/abs-opds
- abs-audioknihi: https://github.com/DeXP/abs-audioknihi
- Abs-Ximalaya: https://github.com/shanyan-wcx/Abs-Ximalaya
- abs-ranobedb: https://github.com/kennethsible/abs-ranobedb
- Czech metadata provider: https://github.com/stecik/audiobookshelf_czech_metadata
- Podium metadata provider: https://github.com/lkiesow/abs-metadata-podium
- AudioSilo Meta: https://github.com/KodeStar/audiosilo-meta

## Caution

Provider repositories and hosted endpoints can change independently. The CLI design intentionally keeps provider instances and URLs in configuration rather than compiling current community endpoints as immutable behavior.
