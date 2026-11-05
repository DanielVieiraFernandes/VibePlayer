#!/usr/bin/env bash
# Gera o .deb a partir da saída do dotnet publish (spec 0009).
#
# O pacote existe por causa do VLC: o app usa o do sistema, e só um pacote nativo consegue declarar
# isso para o apt instalá-lo junto. Não há scripts postinst nem postrm, então nem um "apt purge"
# chega perto da pasta de dados do usuário.
#
# O Depends traz libvlc-dev, e não só libvlc5, porque o LibVLCSharp procura o nome libvlc.so, e é esse
# pacote que cria o link: só com libvlc5 existe apenas libvlc.so.5, e o app acusa VLC ausente.
#
# Uso: build-deb.sh <pasta do publish> <pasta de saída> <versão>
set -euo pipefail

publish="${1:?Informe a pasta do publish}"
output="${2:?Informe a pasta de saída}"
version="${3:?Informe a versão}"

here="$(cd "$(dirname "$0")" && pwd)"
root="$(mktemp -d)"
trap 'rm -rf "$root"' EXIT

mkdir -p "$root/DEBIAN" "$root/opt/vibeplayer" "$root/usr/bin" "$root/usr/share/applications" "$output"
cp -R "$publish"/. "$root/opt/vibeplayer/"
chmod -R u+rwX,go+rX,go-w "$root/opt/vibeplayer"
chmod 755 "$root/opt/vibeplayer/VibePlayer"
# mktemp cria a pasta com 700, que iria para a raiz do pacote.
chmod 755 "$root"
ln -s ../../opt/vibeplayer/VibePlayer "$root/usr/bin/vibeplayer"
install -m 644 "$here/vibeplayer.desktop" "$root/usr/share/applications/vibeplayer.desktop"

# "~" ordena antes de tudo no dpkg: 0.3.1~alpha.0.2 fica abaixo de 0.3.1, como deve ser.
package_version="${version//-/\~}"
size="$(du -sk "$root/opt" | cut -f1)"
sed -e "s/@VERSION@/$package_version/" -e "s/@INSTALLED_SIZE@/$size/" "$here/control" > "$root/DEBIAN/control"

dpkg-deb --root-owner-group --build "$root" "$output/VibePlayer-$version-linux-x64.deb"
