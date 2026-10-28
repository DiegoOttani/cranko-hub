#!/usr/bin/env bash
# Instala o CrankoHub para o usuário atual (sem sudo) e cria o atalho no menu do Cinnamon.
# Uso: extraia o CrankoHub-linux-x64.tar.gz e rode ./instalar.sh dentro da pasta extraída.
set -euo pipefail

origem="$(cd "$(dirname "$0")" && pwd)"
destino="$HOME/.local/share/CrankoHub/app"

# A janela usa o WebKitGTK; no Mint com Cinnamon ele já vem instalado
if ! ldconfig -p | grep -q 'libwebkit2gtk-4.1'; then
  echo "Falta o WebKitGTK. Instale com:  sudo apt install libwebkit2gtk-4.1-0"
  exit 1
fi

if [ "$origem" != "$destino" ]; then
  mkdir -p "$destino"
  cp -a "$origem/." "$destino/"
fi
chmod +x "$destino/CrankoHub"

mkdir -p "$HOME/.local/share/applications"
sed "s|CAMINHO|$destino|g" "$destino/crankohub.desktop" > "$HOME/.local/share/applications/crankohub.desktop"

echo "CrankoHub instalado em $destino"
echo "Procure por \"CrankoHub\" no menu do Cinnamon."
