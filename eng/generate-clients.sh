#!/usr/bin/env bash
set -euo pipefail

dotnet tool restore
dotnet kiota generate \
  -d docs/tasks/initial/bookmeta-cli-spec/sources/libexdb-openapi.json \
  -l CSharp -o src/AudioBookMeta.Tool/Generated/Libex \
  -n AudiobookMeta.Tool.Generated.Libex -c LibexApiClient --tam Internal --ebc --co \
  -i '/search' -i '/quick-search' -i '/book/{asin}' -i '/health'

dotnet kiota generate \
  -d https://meta.audiosilo.app/api/v1/openapi.json \
  -l CSharp -o src/AudioBookMeta.Tool/Generated/AudioSilo \
  -n AudiobookMeta.Tool.Generated.AudioSilo -c AudioSiloApiClient --tam Internal --ebc --co \
  -i '/api/v1/search' -i '/api/v1/lookup' -i '/api/v1/works/{id}' -i '/healthz'
