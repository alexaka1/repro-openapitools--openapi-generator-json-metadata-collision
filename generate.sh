#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

image="${1:-openapitools/openapi-generator-cli:v7.26.0}"
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/local" "$image" validate -i /local/schema.yaml
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/local" "$image" generate \
  -g csharp --library generichost \
  -i /local/schema.yaml -o /local/generated \
  --additional-properties packageName=ReproClient,targetFramework=net10.0,useSourceGeneration=true \
  --global-property apiTests=false,modelTests=false
