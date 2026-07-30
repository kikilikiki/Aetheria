#!/bin/sh
# Construit aetheria-client_0.2.0_amd64.deb à partir du dossier aetheria-client-deb/ déjà
# préparé (binaire Linux x64 self-contained déjà copié dans opt/aetheria/, voir
# ../../Docs/README.md — section "Paquet Linux"). Nécessite dpkg-deb (paquet Debian standard,
# absent de l'environnement Windows qui a produit ce dossier — à exécuter sur une machine
# Linux, WSL, ou un conteneur Docker debian/ubuntu).
set -e
cd "$(dirname "$0")"

chmod +x aetheria-client-deb/opt/aetheria/Aetheria.Client
chmod +x aetheria-client-deb/usr/bin/aetheria
chmod -R go-w aetheria-client-deb

dpkg-deb --build --root-owner-group aetheria-client-deb aetheria-client_0.2.0_amd64.deb
echo "Paquet construit : aetheria-client_0.2.0_amd64.deb"
