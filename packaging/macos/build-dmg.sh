#!/usr/bin/env bash
# Monta o VibePlayer.app a partir da saída do dotnet publish e gera o .dmg (spec 0009).
#
# A saída do publish vai inteira para Contents/MacOS: o VLC e o Whisper procuram os nativos a partir
# do executável, e essa é a estrutura que a documentação do Avalonia recomenda.
#
# Uso: build-dmg.sh <pasta do publish> <pasta de saída> <versão> <X.Y.Z> <rid>
set -euo pipefail

publish="${1:?Informe a pasta do publish}"
output="${2:?Informe a pasta de saída}"
version="${3:?Informe a versão}"
numeric="${4:?Informe a versão numérica}"
rid="${5:?Informe o RID}"

here="$(cd "$(dirname "$0")" && pwd)"
staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT
app="$staging/VibePlayer.app"

mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$output"
cp -R "$publish"/. "$app/Contents/MacOS/"
chmod +x "$app/Contents/MacOS/VibePlayer"

# Os avisos de licença são documentos, não código: dentro de MacOS, atrapalhariam a assinatura.
for notice in LICENSE THIRD-PARTY-NOTICES.md licenses; do
  mv "$app/Contents/MacOS/$notice" "$app/Contents/Resources/"
done

sed "s/@NUMERIC_VERSION@/$numeric/g" "$here/Info.plist" > "$app/Contents/Info.plist"

# Assinatura ad-hoc: não identifica o autor nem evita o Gatekeeper, mas reorganizar os arquivos
# invalida a do publish, e o Apple Silicon não executa código sem assinatura válida.
codesign --force --deep --sign - "$app"

ln -s /Applications "$staging/Applications"
hdiutil create -volname "VibePlayer" -srcfolder "$staging" -ov -format UDZO \
  "$output/VibePlayer-$version-$rid.dmg"
