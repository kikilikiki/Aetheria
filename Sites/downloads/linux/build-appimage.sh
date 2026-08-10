#!/bin/sh
# Construit Aetheria-x86_64.AppImage à partir de Aetheria.AppDir/ déjà préparé (Launcher +
# Client Linux x64 self-contained déjà copiés dans usr/bin/, voir ../../README.md — section
# "Paquet Linux"). Nécessite appimagetool (https://github.com/AppImage/AppImageKit/releases),
# absent de l'environnement Windows qui a produit ce dossier — à exécuter sur une machine Linux.
#
# TODO : Aetheria.AppDir/aetheria.png (icône) n'existe pas encore — appimagetool refusera de
# construire l'AppImage tant qu'aucune icône n'est fournie (voir Icon= dans aetheria.desktop).
set -e
cd "$(dirname "$0")"

chmod +x Aetheria.AppDir/AppRun
chmod +x Aetheria.AppDir/usr/bin/Aetheria.Launcher
chmod +x Aetheria.AppDir/usr/bin/Aetheria.Client

appimagetool Aetheria.AppDir Aetheria-x86_64.AppImage
echo "AppImage construite : Aetheria-x86_64.AppImage"
