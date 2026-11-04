#!/usr/bin/env bash
# Baixa o menor modelo do Whisper para os testes de integração, conferindo o hash: um arquivo
# trocado no servidor deve falhar aqui, e não como um teste de transcrição inexplicável.
#
# Uso: download-whisper-test-model.sh <arquivo>
set -euo pipefail

target="${1:?Informe o arquivo de destino}"
url="https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-tiny-q5_1.bin"
expected="818710568da3ca15689e31a743197b520007872ff9576237bda97bd1b469c3d7"

if [ ! -f "$target" ]; then
  mkdir -p "$(dirname "$target")"
  curl --fail --location --silent --show-error --retry 3 --output "$target" "$url"
fi

# O macOS tem shasum, e não sha256sum.
if command -v sha256sum > /dev/null; then
  actual="$(sha256sum "$target" | cut -d' ' -f1)"
else
  actual="$(shasum -a 256 "$target" | cut -d' ' -f1)"
fi

if [ "$actual" != "$expected" ]; then
  echo "::error::Hash inesperado no modelo do Whisper: $actual"
  rm -f "$target"
  exit 1
fi
