# Normalized output schema

## 1. Stability

Machine output is versioned independently of providers. `schema_version: 1` is the first stable shape.

## 2. JSON document

```json
{
  "schema_version": 1,
  "request": {
    "query": "proj hail",
    "author": "weir",
    "providers": ["libex", "audiosilo"]
  },
  "results": [],
  "provider_status": [],
  "warnings": []
}
```

## 3. Search result

Canonical fields:

```json
{
  "provider": "libex",
  "provider_type": "libex",
  "provider_record_id": "B08G9PRS1K",
  "title": "Project Hail Mary",
  "subtitle": null,
  "authors": ["Andy Weir"],
  "narrators": ["Ray Porter"],
  "series": [],
  "identifiers": {
    "asin": ["B08G9PRS1K"],
    "isbn10": [],
    "isbn13": [],
    "other": {}
  },
  "publisher": null,
  "published_year": null,
  "release_date": null,
  "language": "english",
  "duration_seconds": null,
  "genres": [],
  "tags": [],
  "cover_url": null,
  "description": null,
  "source_url": null,
  "score": 97.4,
  "confidence": "high",
  "work_cluster_id": "...",
  "edition_cluster_id": "...",
  "warnings": []
}
```

The example is illustrative, not a claim about a live record.

## 4. Provider status

```json
{
  "provider": "goodreads",
  "status": "timeout",
  "elapsed_ms": 4001,
  "candidate_count": 0,
  "request_count": 1,
  "message": "provider timed out"
}
```

Status values: `ok`, `empty`, `partial`, `error`, `timeout`, `rate_limited`, `skipped`.

## 5. Raw data

`raw` is absent unless `--raw` is set. When present it is provider namespaced and size-limited. Consumers MUST NOT rely on its shape.

## 6. JSONL

Each result/group is one JSON object. Provider diagnostics go to stderr by default so stdout remains stream-processable. A future `--jsonl-diagnostics` flag may emit typed diagnostic records.
