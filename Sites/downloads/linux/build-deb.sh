#!/bin/sh
# Construit aetheria_<version>_amd64.deb à partir du dossier aetheria-client-deb/ déjà
# préparé (Launcher + Client Linux x64 self-contained déjà copiés dans opt/aetheria/, voir
# ../../README.md — section "Paquet Linux"). Nécessite dpkg-deb (paquet Debian standard,
# absent de l'environnement Windows qui a produit ce dossier — à exécuter sur une machine
# Linux, WSL, ou un conteneur Docker debian/ubuntu).
set -e
cd "$(dirname "$0")"

VERSION="$(sed -n 's/^Version: //p' aetheria-client-deb/DEBIAN/control)"

chmod +x aetheria-client-deb/opt/aetheria/Aetheria.Launcher
chmod +x aetheria-client-deb/opt/aetheria/Aetheria.Client
chmod +x aetheria-client-deb/usr/bin/aetheria
chmod -R go-w aetheria-client-deb

dpkg-deb --build --root-owner-group aetheria-client-deb "aetheria_${VERSION}_amd64.deb"
echo "Paquet construit : aetheria_${VERSION}_amd64.deb"
