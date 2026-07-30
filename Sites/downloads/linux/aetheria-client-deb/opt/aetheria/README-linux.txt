Aetheria — build Linux (Client uniquement)
============================================

Le moteur de jeu (Aetheria.Client, rendu OpenGL via Silk.NET) est multiplateforme et
tourne nativement sur Linux. Le Launcher (création de compte, connexion, sélection de
personnage, mise à jour automatique) est en revanche écrit en WPF, une interface
réservée à Windows — il n'existe donc pas encore de Launcher Linux.

Sans Launcher, Aetheria.Client démarre en mode démo hors-ligne (aucune connexion à un
vrai compte). Pour se connecter à un serveur avec un jeton de session déjà obtenu
autrement (ex. compte de test, script), utilisez :

    ./Aetheria.Client --token="<jeton>" --characterId="<id-personnage>" --host="<adresse-du-serveur>"

Porter le Launcher vers une interface multiplateforme (Avalonia UI, par exemple) pour
un vrai support Linux avec connexion/inscription est un chantier séparé, pas encore
réalisé.
