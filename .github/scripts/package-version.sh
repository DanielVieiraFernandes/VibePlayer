#!/usr/bin/env bash
# Lê a versão do MinVer, que é a mesma gravada nos binários, para os instaladores nunca terem número
# escrito em arquivo. Imprime variáveis no formato do $GITHUB_ENV:
#   PACKAGE_VERSION          0.3.0 ou 0.3.1-alpha.0.2
#   PACKAGE_NUMERIC_VERSION  0.3.0 ou 0.3.1 (Windows e macOS só aceitam números nos metadados)
#
# Na release, informe a versão da tag: se o MinVer divergir dela, é melhor falhar aqui do que
# publicar um pacote com a versão errada.
#
# Uso: package-version.sh [versão esperada] >> "$GITHUB_ENV"
set -euo pipefail

expected="${1:-}"
project="src/VibePlayer.App/VibePlayer.App.csproj"

# O alvo do MinVer só existe depois do restore. A saída vai para stderr: stdout é só das variáveis.
dotnet restore "$project" --verbosity quiet >&2
version="$(dotnet msbuild "$project" -target:MinVer -getProperty:MinVerVersion | tr -d '\r')"

if ! [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+ ]]; then
  echo "::error::Versão inesperada do MinVer: '$version'" >&2
  exit 1
fi

if [ -n "$expected" ] && [ "$version" != "$expected" ]; then
  echo "::error::O MinVer calculou $version, mas a release é $expected." >&2
  exit 1
fi

echo "PACKAGE_VERSION=$version"
echo "PACKAGE_NUMERIC_VERSION=${version%%-*}"
