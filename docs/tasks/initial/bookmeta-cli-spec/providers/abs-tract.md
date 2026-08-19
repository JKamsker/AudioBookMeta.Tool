# abs-tract

Repository: `https://github.com/ahobsonsayers/abs-tract`

Recommended adapter: generic `abs`, configured as separate provider instances for Goodreads and Kindle.

## Sources

### Goodreads

Documented metadata:

- title;
- author;
- cover (repository warns quality can be low or missing);
- original publish year;
- series name and position;
- description;
- top genres;
- ISBN when available;
- publisher;
- language.

Search example shape: `/goodreads/search?query=The+Hobbit&author=J.R.R.+Tolkien`.

ABS base URL: `/goodreads`. Authentication is documented as unset.

The repository notes Goodreads search quality can be poor and that the legacy API is deprecated. Local CLI reranking is therefore especially useful.

### Kindle

Documented metadata:

- title;
- author;
- high-quality cover;
- edition publish year;
- ASIN.

ABS base URL: `/kindle/<region>`.

Documented regions: `au`, `ca`, `de`, `es`, `fr`, `in`, `it`, `jp`, `uk`, `us`.

Authentication is documented as unset.

## CLI configuration pattern

```toml
[providers.goodreads_abstract]
type = "abs"
base_url = "http://host:5555/goodreads"

[providers.kindle_us]
type = "abs"
base_url = "http://host:5555/kindle/us"
```

## Incomplete search

Upstream incomplete/fuzzy semantics are not established strongly enough by the reviewed README to guarantee them. Use one provider request plus local incomplete-query ranking.
