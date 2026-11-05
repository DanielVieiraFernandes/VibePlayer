#!/usr/bin/env bash
# Testa o .dmg do jeito que o usuário instala: monta, copia para Aplicativos, roda e remove.
#
# Uso: smoke-test-macos.sh <arquivo.dmg> <X.Y.Z> <rid> <modelo> <áudio>
set -euo pipefail

dmg="${1:?Informe o .dmg}"
numeric="${2:?Informe a versão numérica}"
rid="${3:?Informe o RID}"
model="${4:?Informe o modelo do Whisper}"
fixture="${5:?Informe o áudio de amostra}"

here="$(cd "$(dirname "$0")" && pwd)"
mountpoint="$(mktemp -d)"
installed="/Applications/VibePlayer.app"

cleanup() {
  hdiutil detach "$mountpoint" -quiet || true
  rm -rf "$installed"
}
trap cleanup EXIT

hdiutil attach "$dmg" -nobrowse -readonly -mountpoint "$mountpoint" -quiet
[ -L "$mountpoint/Applications" ] || { echo "::error::O .dmg não tem o atalho para Aplicativos."; exit 1; }

ditto "$mountpoint/VibePlayer.app" "$installed"

if ! codesign --verify --deep --strict "$installed"; then
  echo "::error::Assinatura inválida em VibePlayer.app."
  exit 1
fi

for notice in LICENSE THIRD-PARTY-NOTICES.md licenses/LGPL-2.1.txt; do
  [ -f "$installed/Contents/Resources/$notice" ] || { echo "::error::Faltou $notice no .app."; exit 1; }
done

actual="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$installed/Contents/Info.plist")"
if [ "$actual" != "$numeric" ]; then
  echo "::error::O Info.plist diz $actual, e a versão é $numeric."
  exit 1
fi

# O runner é Apple Silicon: o pacote x64 só executa via Rosetta. Sem ela, fica a conferência acima
# e o roteiro manual.
if [ "$rid" = "osx-x64" ] && ! arch -x86_64 /usr/bin/true 2> /dev/null; then
  echo "::warning::Sem Rosetta no runner: a transcrição do pacote x64 não foi testada."
else
  pwsh -NoProfile -File "$here/smoke-transcription.ps1" \
    -Executable "$installed/Contents/MacOS/VibePlayer" -Fixture "$fixture" -Model "$model"
fi

echo "Instalador conferido."
