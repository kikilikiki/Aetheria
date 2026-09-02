Aetheria — build Linux (Launcher + Client)
============================================

Le Launcher (compte, connexion, mise à jour automatique) est desormais multiplateforme
(porté vers Avalonia UI, voir Sites/README.md — section "Paquet Linux") — plus besoin de
passer par le Client seul pour se connecter avec un vrai compte :

    ./Aetheria.Launcher

Le Launcher lance ensuite ./Aetheria.Client à côté de lui avec le jeton de session
obtenu. Pour se connecter directement avec le Client, sans passer par le Launcher (ex.
compte de test, script), le mode manuel reste disponible :

    ./Aetheria.Client --token="<jeton>" --characterId="<id-personnage>" --host="<adresse-du-serveur>"

Sans argument ni Launcher, Aetheria.Client démarre en mode démo hors-ligne (aucune
connexion à un vrai compte).
