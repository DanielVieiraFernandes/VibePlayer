#!/usr/bin/env bash
# Valida a versão pedida para uma release ou hotfix antes de criar qualquer branch.
# Uso: validate-version.sh <X.Y.Z> <release|hotfix>
#
# Falha se: a versão não for X.Y.Z; a tag vX.Y.Z ou a branch <tipo>/X.Y.Z já existirem;
# ou a versão não for maior que a última release publicada (evita publicar "para trás").
set -euo pipefail

version="${1:-}"
kind="${2:-}"

if ! [[ "$version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "::error::Versão inválida: '$version'. Use o formato X.Y.Z, por exemplo 0.1.0."
  exit 1
fi

if git ls-remote --tags origin "refs/tags/v$version" | grep -q .; then
  echo "::error::A tag v$version já existe: essa versão já foi publicada."
  exit 1
fi

if git ls-remote --heads origin "refs/heads/$kind/$version" | grep -q .; then
  echo "::error::A branch $kind/$version já existe."
  exit 1
fi

latest="$(git tag --list 'v*' | sed 's/^v//' | grep -E '^[0-9]+\.[0-9]+\.[0-9]+$' | sort -V | tail -n 1 || true)"
if [ -n "$latest" ]; then
  highest="$(printf '%s\n%s\n' "$latest" "$version" | sort -V | tail -n 1)"
  if [ "$highest" != "$version" ] || [ "$latest" = "$version" ]; then
    echo "::error::A versão $version precisa ser maior que a última publicada ($latest)."
    exit 1
  fi
fi

echo "Versão $version válida para $kind (última publicada: ${latest:-nenhuma})."
