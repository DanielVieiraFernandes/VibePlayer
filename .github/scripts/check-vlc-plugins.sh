#!/usr/bin/env bash
# Confere que só plugins da lista de permissão do VLC estão na pasta informada (saída do build ou
# do publish). A lista vive em src/VibePlayer.App/VlcPlugins.targets, e esta cópia é gerada dela:
# um plugin GPL que escape do filtro (ex.: depois de atualizar o pacote do VLC) falha aqui, e não
# depois de distribuído.
#
# Uso: check-vlc-plugins.sh <pasta>
set -euo pipefail

folder="${1:?Informe a pasta do build ou do publish}"
allowed="$(dirname "$0")/vlc-allowed-plugins.txt"

# Laço de read, e não mapfile: o bash do macOS é o 3.2, que não tem mapfile.
plugins=()
while IFS= read -r plugin; do
  plugins+=("$plugin")
done < <(find "$folder" -type f -path '*libvlc*' -path '*/plugins/*' \( -name '*.dll' -o -name '*.dylib' \))

# No Linux o app usa o VLC do sistema, e o pacote não leva plugins.
if [ "${#plugins[@]}" -eq 0 ]; then
  if [ "$(uname -s)" = "Linux" ]; then
    echo "Sem plugins do VLC (Linux usa o VLC do sistema)."
    exit 0
  fi

  echo "::error::Nenhum plugin do VLC em '$folder': o filtro removeu tudo ou o caminho mudou."
  exit 1
fi

rejected=0
for plugin in "${plugins[@]}"; do
  name="$(basename "$plugin")"
  name="${name%.*}"
  if ! tr -d '\r' < "$allowed" | grep -qxF "$name"; then
    echo "::error::Plugin do VLC fora da lista de permissão: $plugin"
    rejected=$((rejected + 1))
  fi
done

[ "$rejected" -eq 0 ] || exit 1
echo "${#plugins[@]} plugins do VLC conferidos com a lista de permissão."
