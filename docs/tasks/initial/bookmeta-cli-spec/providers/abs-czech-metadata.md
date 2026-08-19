# audiobookshelf_czech_metadata

Repository: `https://github.com/stecik/audiobookshelf_czech_metadata`

Recommended adapter: generic `abs`, either aggregate root or source-specific base paths.

## Scope

FastAPI Audiobookshelf metadata provider for Czech audiobook storefronts. The reviewed README lists:

- Alza Audioknihy;
- Albatros Media Audioknihy;
- Audiolibrix Czech;
- Audioteka Czech;
- Databaze knih (book-metadata fallback, disabled by default globally);
- Kanopa;
- Knihy Dobrovský Audioknihy;
- Kosmas Audioknihy;
- Luxor Audioknihy;
- Megaknihy Audioknihy;
- Naposlech;
- OneHotBook;
- O2 Knihovna Audioknihy;
- Palmknihy Audioknihy;
- ProgresGuru Audioknihy;
- Radioteka;
- Rozhlas Hry a audioknihy.

The service searches configured sources, ranks results, normalizes them into `{"matches": [...]}`, and returns them to ABS.

## Global and source-specific bases

Root aggregate base: `http://host:8000` in the documented local example.

Source-specific bases include:

`/alza`, `/albatrosmedia`, `/audiolibrix`, `/audioteka`, `/databazeknih`, `/kanopa`, `/knihydobrovsky`, `/kosmas`, `/luxor`, `/megaknihy`, `/naposlech`, `/onehotbook`, `/o2knihovna`, `/palmknihy`, `/progresguru`, `/radioteka`, `/rozhlas`.

This makes it possible to configure either one aggregate CLI provider or multiple selectable source-specific instances.

## Runtime settings documented

- application/port/log settings;
- `REQUEST_TIMEOUT_SECONDS`;
- `SCRAPER_TIMEOUT_SECONDS`;
- optional `AUDIOBOOKSHELF_AUTH_TOKEN`;
- per-source `ENABLE_*` switches.

If auth token is set, the provider accepts the exact value in `AUTHORIZATION` and also Bearer form according to the README.

## Time budget

The repository warns Audiobookshelf itself has a roughly 10-second metadata timeout and recommends scraper budgets that keep individual sources from blocking the entire aggregate request. `bookmeta` should still enforce its own provider deadline.

## Incomplete search

The provider itself ranks matches, but the reviewed docs do not define a universal fuzzy algorithm across all scrapers. Keep local ranking enabled.
