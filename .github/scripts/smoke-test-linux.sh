#!/usr/bin/env bash
# Testa o .deb numa máquina sem VLC: se faltar algo em Depends, a transcrição falha aqui. Por isso
# este job não instala o VLC antes, ao contrário da CI comum.
#
# Para não depender de uma release anterior, gera um .deb da versão 0.0.1 com os mesmos arquivos e
# testa a atualização em toda execução.
#
# Uso: smoke-test-linux.sh <arquivo.deb> <pasta do publish> <modelo> <áudio>
set -euo pipefail

deb="$(realpath "${1:?Informe o .deb}")"
publish="${2:?Informe a pasta do publish}"
model="${3:?Informe o modelo do Whisper}"
fixture="${4:?Informe o áudio de amostra}"

here="$(cd "$(dirname "$0")" && pwd)"
data_dir="$HOME/.config/VibePlayer"
marker="$data_dir/marcador-da-ci.txt"

installed_version() {
  dpkg-query --show --showformat='${Version}' vibeplayer 2> /dev/null || true
}

if ldconfig -p | grep -q 'libvlc\.so'; then
  echo "::error::O runner já tem o VLC: este teste não provaria as dependências do .deb."
  exit 1
fi

old_output="$(mktemp -d)"
bash "$here/../../packaging/linux/build-deb.sh" "$publish" "$old_output" 0.0.1

sudo apt-get update --quiet
sudo apt-get install --yes --quiet "$old_output/VibePlayer-0.0.1-linux-x64.deb"
[ "$(installed_version)" = "0.0.1" ] || { echo "::error::A versão antiga não ficou instalada."; exit 1; }

mkdir -p "$data_dir"
echo "dados do usuário" > "$marker"

sudo apt-get install --yes --quiet "$deb"
new_version="$(installed_version)"
if [ "$new_version" = "0.0.1" ] || [ -z "$new_version" ]; then
  echo "::error::A atualização não trocou a versão."
  exit 1
fi

command -v vibeplayer > /dev/null || { echo "::error::Comando vibeplayer não encontrado."; exit 1; }
[ -f /usr/share/applications/vibeplayer.desktop ] || { echo "::error::Faltou a entrada do menu."; exit 1; }

for notice in LICENSE THIRD-PARTY-NOTICES.md licenses/LGPL-2.1.txt; do
  [ -f "/opt/vibeplayer/$notice" ] || { echo "::error::Faltou $notice na instalação."; exit 1; }
done

# Pelo link em /usr/bin, que é como o usuário abre o app.
pwsh -NoProfile -File "$here/smoke-transcription.ps1" \
  -Executable "$(command -v vibeplayer)" -Fixture "$fixture" -Model "$model"

sudo apt-get purge --yes --quiet vibeplayer
[ ! -e /opt/vibeplayer ] || { echo "::error::O purge deixou /opt/vibeplayer."; exit 1; }
[ ! -e /usr/bin/vibeplayer ] || { echo "::error::O purge deixou o comando vibeplayer."; exit 1; }
[ -f "$marker" ] || { echo "::error::O purge mexeu na pasta de dados."; exit 1; }

echo "Instalador conferido."
