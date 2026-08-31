# Idées

Notes en vrac, à trier plus tard.

> Relu le 2026-08-26 en inspectant le code réel (grep + lecture directe, `git log` sur les
> 203 commits `H1`→`H136`), pas seulement `Docs/README.md` qui est en net retard sur le projet.
> Beaucoup de choses que je croyais manquantes existent déjà : mode Coopération/combat de groupe
> partagé, terrain/obstacles/combos/affinités élémentaires en combat, élevage + fusion de
> monstres, disposition en grille des donjons (façon Binding of Isaac), téléporteur entre les
> 4 royaumes, musique en jeu, guerre de royaumes avec capture de territoire dynamique et
> résolution hebdo automatique, classement PvP, tchat privé, Launcher multiplateforme
> (Windows/Linux) avec mise à jour auto. Les idées ci-dessous ne reprennent que ce qui reste
> vérifiablement absent ou volontairement simplifié dans le code à cette date. Chaque idée est
> suivie d'une **Proposition** : une piste concrète, pas une décision arrêtée.

> Complété le 2026-08-31 après la refonte des donjons (`H142` : 2 portails niveau 1 + niveau
> aléatoire en rotation horaire, nom/niveau affichés au-dessus, entrée directe, choix de
> modificateur Normal/Hardcore/Spécial saison à l'entrée) et l'ajout des outils admin sur les
> variantes de créature (`H142` : faire apparaître un combat, modifier un monstre possédé,
> `MonsterVariantCatalog.TryParse`). Les idées marquées **(2026-08-31)** en découlent ou comblent
> un manque repéré à cette occasion.

## Gameplay

- **Arbre de talents/compétences général** — explicitement mis de côté lors de l'ajout du
  prestige (`H62` : "Arbre de talents non implémenté cette passe, scope réduit"). Le système de
  Natures (`MonsterNature`) fait déjà varier les stats à la Pokémon, mais aucune progression de
  compétences choisie par le joueur n'existe.
  **Proposition** : un arbre par créature (pas par personnage, cohérent avec "4 créatures max en
  combat"), quelques dizaines de nœuds max pour rester lisible, débloqués avec des points gagnés
  par montée de niveau. Chaque nœud = un petit bonus passif (+% dégâts, +% PV, réduction de
  cooldown de la capacité spéciale) ou, en fin d'arbre, une variante de la capacité spéciale
  existante plutôt qu'une nouvelle capacité inventée de zéro — plus simple à équilibrer.
  Nouveau panneau dédié (touche libre, ex. `Y`), rendu comme une grille de nœuds reliés par des
  lignes, réutilisable pour l'écran de fusion/reproduction existant si le style visuel convient.

- **Capacité spéciale dédiée pour Tank/Assassin/Support/Invocateur/Berserker**
  (`Shared/Enums/MonsterType.cs`) — seuls Soigneur, Archer, Mage et Contrôleur ont un
  comportement propre dans `CombatEngine.ResolveSpecialAbility` ; les 5 autres retombent sur le
  "coup puissant" générique.
  **Proposition**, une capacité par rôle cohérente avec son nom : Tank = provoque (force les
  ennemis proches à le cibler la manche suivante) + réduction de dégâts subis temporaire ;
  Assassin = dégâts critiques garantis si la cible est déjà sous 50 % PV ; Support = buff
  temporaire de stats sur un allié (symétrique du Soigneur qui soigne) ; Invocateur = pose un
  obstacle "invocation" qui attaque pour lui (réutilise le système d'obstacles déjà existant du
  Contrôleur, juste avec un comportement offensif au lieu de bloquant) ; Berserker = dégâts
  infligés croissants à mesure que ses propres PV baissent (mécanique de rage, pas de nouveau
  système de ressource à créer).

- **Garde-fou anti-auto-appairage en arène** : `ArenaQueueService.EnqueueAndTryMatch` ne
  déduplique que par `CharacterId`, pas par compte (`UserId`) — deux personnages du même compte
  peuvent aujourd'hui se retrouver appairés l'un contre l'autre.
  **Proposition** : indexer aussi la file par `UserId` et refuser de former un match où le même
  `UserId` apparaît des deux côtés — swap avec le prochain ticket en attente plutôt que d'annuler
  le matchmaking (évite de pénaliser tout le monde pour un seul conflit).

- **Vrai lobby d'arène** (inviter des amis nommément dans une équipe) — la file reste en ordre
  d'arrivée uniquement (`ArenaQueueService`).
  **Proposition** : réutiliser le système de groupe existant (code à 5 chiffres déjà en place
  pour les groupes PvE) — un groupe complet peut s'inscrire ensemble dans la file d'arène comme
  une seule entité, plutôt que de recréer un système d'invitation séparé.

- **Réajustement du nombre d'ennemis si un membre rejoint un combat de groupe déjà démarré**, et
  **verrou contre la double création de combat** si deux membres engagent au même instant
  (fenêtre de course très étroite) — les deux simplifications sont documentées telles quelles
  dans `CombatService.cs`.
  **Proposition** : pour la double création, un verrou léger par `PartyId` (dictionnaire de
  `SemaphoreSlim` en mémoire, même style que `CombatSessionStore`) le temps de créer la session.
  Pour le réajustement, plus simple à assumer qu'à corriger : refuser explicitement à un membre
  de rejoindre un combat déjà en cours plutôt que de recalculer le nombre d'ennemis à la volée
  (message clair "combat déjà engagé, rejoignez le suivant").

- **Pathfinding évitant les obstacles** : toujours "un pas naïf vers la cible" pour l'IA de
  combat (`CombatEngine.cs`, commentaire explicite) et toujours un chemin orthogonal direct pour
  le déplacement en extérieur.
  **Proposition** : BFS sur la grille de combat (7x7, petit, un vrai A* serait disproportionné)
  pour l'IA — évite qu'un monstre reste bloqué contre un obstacle destructible sans l'attaquer.
  Côté extérieur, BFS sur la grille de la carte en excluant les cases occupées par des bâtiments,
  réutilisable pour le calcul du chemin au clic déjà en place.

- **Probabilité de rencontre sauvage dépendante du biome/terrain** (aujourd'hui une constante,
  `WildEncounterChance = 0.11` dans `Client/Program.cs`, indépendante du terrain traversé).
  **Proposition** : table de multiplicateurs par type de case déjà distingué dans `WorldMap`
  (herbe claire/moyenne/foncée, chemin = 0, étang = 0 sauf monture aquatique) — pas besoin de
  nouveau système de biome, juste brancher le taux sur le type de case déjà calculé.

- **Effets différenciés par objet donné à une créature** — `MonsterCareService` : tout objet
  donné accorde le même montant d'XP fixe, pas d'effet propre par objet (stats, évolution, ...).
  **Proposition** : ajouter un champ `GiveEffect` (enum : XP/BonusStat/DéclencheÉvolution) sur
  `ItemEntity`, réutilisable pour le catalogue déjà riche (`EquipmentCatalogSeeder`) sans créer un
  second système d'objets — un "Fruit de force" donnerait +1 Attaque permanent au lieu d'XP.

- **Contrepartie en créature côté joueur ciblé dans l'Échange** (`TradeService`) — aujourd'hui
  l'initiateur peut proposer une de ses créatures, mais ce qu'on lui demande en retour est
  toujours de l'or, jamais une créature du joueur visé.
  **Proposition** : étendre `ProposeTradeRequest` avec un `RequestedMonsterId` optionnel (à la
  place ou en plus de l'or demandé) ; le joueur ciblé voit l'offre complète et accepte/refuse en
  un clic, pas de contre-proposition libre (garde le système simple, cohérent avec le choix
  existant de ne pas faire un vrai système d'enchère de trade).

- **Table de butin dédiée aux matériaux de boss/essences** — `EquipmentCatalogSeeder` : ce sont
  pour l'instant de simples objets "Ressource" tirés du même butin aléatoire que le reste, la
  distinction "matériau de boss" est purement narrative.
  **Proposition** : une table de butin spécifique déclenchée uniquement sur les salles Boss/Boss
  légendaire (déjà distinguées côté `DungeonRoomService`), avec un taux garanti pour l'essence du
  boss concerné plutôt qu'un tirage générique — donne enfin un sens mécanique au farm de boss.

- **Embranchements/choix dans la chaîne de quêtes tutoriel** — volontairement une seule quête
  active à la fois, en séquence linéaire (`QuestEntity.SequenceOrder`, `QuestCatalogSeeder`).
  **Proposition** : à ne faire qu'après la quête tutoriel elle-même terminée et stable — ajouter
  un champ `ChoiceNextQuestId` optionnel sur `QuestEntity` pour un embranchement ponctuel (pas un
  arbre de dialogue complet), affiché comme un choix à deux boutons dans la boîte de dialogue PNJ
  déjà existante.

- **Vraie géographie pour les îles volantes/aquatiques** (`ExplorationService`) — l'accès est
  aujourd'hui vérifié par la possession d'une monture adaptée et débloque un succès caché, sans
  nouvelle carte/terrain réel (le moteur n'a pas de notion d'élévation/eau traversable).
  **Proposition** : gros morceau, à ne considérer qu'après le rendu de sprites réels (voir
  Contenu) — une "île" pourrait rester une simple `WorldMap` supplémentaire accessible par un
  point de téléportation dédié (réutilise le mécanisme déjà en place pour voyager entre les
  4 royaumes) plutôt que d'ajouter une vraie notion d'élévation au moteur.

- **Persistance des PV des créatures entre deux combats** (elles repartent toujours à leur
  maximum en début de combat, `CombatService.cs`).
  **Proposition** : à trancher avec le porteur de projet avant de coder — c'est peut-être un
  choix de confort assumé plutôt qu'un oubli (évite la frustration de devoir soigner entre
  chaque rencontre). Si on veut l'ajouter : stocker `CurrentHealth` par créature, régénération
  lente hors combat (par minute réelle) + soin complet gratuit en ville, pour ne pas punir un
  joueur qui enchaîne les rencontres sauvages.

- **(2026-08-31) Modificateur de donjon partagé au sein d'un groupe** — le modificateur
  (Normal/Hardcore/Spécial saison) est choisi par chaque joueur à son entrée
  (`StartDungeonCombatRequest.Modifier`, `EnterDungeonInterior` côté Client). En groupe, deux
  membres pourraient engager le même donjon avec des modificateurs différents, or le combat de
  groupe est partagé (`CombatService` rejoint la session existante).
  **Proposition** : le premier membre qui entre fixe le modificateur pour tout le groupe ; les
  suivants voient ce choix affiché (verrouillé) plutôt qu'un panneau de sélection. Résolu côté
  serveur en lisant `session.RewardMultiplier`/`IsHardcoreCombat` de la session déjà créée quand
  un membre rejoint, plutôt qu'en refaisant confiance au `Modifier` envoyé par le nouvel arrivant.

- **(2026-08-31) Aura / particules pour les variantes rares en combat** — la variante d'un mob
  n'est signalée que par le badge texte de l'encart haut-droite (`DrawCombatEnemyPanel`) et un
  préfixe dans son nom (`CombatService.CreateWildCombatSessionAsync`). Rien sur le portrait
  lui-même.
  **Proposition** : un halo pulsant derrière le portrait ennemi, teinté avec la couleur de la
  définition (`MonsterVariantDefinition.ColorR/G/B`) — réutilise exactement le rendu de l'anneau
  de montée de niveau déjà en place (`activeLevelUpGlows` dans `DrawCombat`), juste rendu en
  continu pour les combattants dont `Variant != Normal`.

- **(2026-08-31) Estimation de puissance recommandée avant d'entrer en donjon** — le panneau de
  choix de modificateur (`DrawDungeonModifierPanel`) affiche le niveau des monstres (`NIV. x-y`)
  mais rien sur la force de l'équipe du joueur face à ça.
  **Proposition** : comparer le niveau moyen de l'équipe active (données déjà chargées via
  `starterApi.GetCharacterMonstersAsync`) au `MinLevel` du donjon et afficher un simple indicateur
  à trois états (« sous-dimensionné » / « adapté » / « surdimensionné »), sans chiffre exact —
  cohérent avec le choix existant de ne pas exposer les formules de stats au Client.

- **(2026-08-31) Défis quotidiens** — `ChallengeCatalog` ne définit que des défis hebdomadaires
  (`ChallengePeriod.Weekly`) et mensuels ; pas de palier quotidien.
  **Proposition** : ajouter `ChallengePeriod.Daily` avec 2-3 défis courts (gagner 3 combats,
  capturer 1 créature, descendre à l'étage 5 d'un donjon). `ChallengeService` prend déjà un
  instantané de statistique par période — il suffit d'ajouter la fenêtre "jour UTC" au calcul de
  réinitialisation existant, aucun nouveau système.

- **(2026-08-31) Mode entraînement contre un mannequin** — tester une composition d'équipe
  impose aujourd'hui d'engager une vraie rencontre (perte de PV, cooldown de donjon, butin).
  **Proposition** : réutiliser exactement le chemin de `CombatService.StartAdminEncounterAsync`
  (ajouté en `H142` pour l'admin) mais accessible à tous depuis un PNJ « Maître d'armes » de la
  capitale : combat contre une créature au niveau/type choisi, sans butin ni XP ni conséquence,
  quittable à tout moment.

## Contenu

- **Contenu de saison réel** (nouveaux monstres, donjons, cosmétiques) au-delà du suivi de
  cycle/récompenses de fin de saison déjà en place.
  **Proposition** : plutôt qu'un système générique, définir dès maintenant le contenu de la
  Saison 2 (2-3 nouvelles espèces par royaume, un donjon exclusif, une poignée de cosmétiques
  pour le Passe de Niveau) — le pipeline (seeders idempotents) existe déjà pour tout le reste du
  catalogue, il suffit d'ajouter du contenu dedans plutôt que d'inventer un nouveau système.

- **(2026-08-31) Vrais modificateurs roguelike pour le « Donjon Sans Fin »** — c'est un donjon
  comme les autres, seul son `MaxMonsterLevel` très haut le distingue (`DungeonSeeder`).
  **Proposition** : uniquement pour ce donjon, tirer un modificateur temporaire tous les 10 étages
  (déjà la cadence des mini-boss côté `DungeonFloorGenerator`) — « dégâts alliés +20 % mais PV
  max -20 % », « une seule créature en combat », « butin doublé, monstres +30 % »… Réutilise la
  mécanique de `SeasonalDungeonModifierCatalog` (`H142` : multiplicateurs simples + description
  affichée), juste tirée par palier plutôt que par saison, et présentée en overlay à l'entrée du
  palier.

- **(2026-08-31) Marchand itinérant à stock tournant** — la Boutique (`shop`) propose un
  catalogue fixe, rien n'incite à repasser régulièrement.
  **Proposition** : un PNJ « Colporteur » qui apparaît dans une capitale au hasard, avec un
  inventaire de 4-5 objets (dont un rare) renouvelé chaque heure UTC — exactement le même
  mécanisme de rotation déterministe par `hourBucket` que `DungeonWorldService.GetActivePortals`
  (`H142`), sans job serveur à ajouter.

- **Vraie récompense mécanique pour les salles Énigme/Piège/Événement/Salle secrète du donjon**
  (au-delà du texte d'ambiance, du coffre et du marchand qui fonctionnent déjà réellement).
  **Proposition**, par salle, sans inventer de mini-jeu complexe : Piège = perte de PV légère
  avant le prochain combat (retenu comme risque, pas juste de l'ambiance) ; Énigme = un simple
  choix binaire avec conséquence (petite récompense ou petit malus) ; Événement = buff temporaire
  aléatoire pour l'étage en cours ; Salle secrète = coffre bonus avec un taux d'objet rare
  supérieur au coffre normal (réutilise `DungeonRoomService.OpenChestAsync` avec un multiplicateur).

- **Vraie scène d'intérieur pour les bâtiments non couverts par `BuildingInteriors` et pour
  l'intérieur des donjons** (toujours un écran à plat, pas une scène isométrique).
  **Proposition** : pas prioritaire tant que les sprites réels (item suivant) ne sont pas là —
  une fois les assets disponibles, réutiliser `IsoMath`/`DrawQuad` déjà en place pour l'extérieur
  plutôt qu'un système de rendu d'intérieur séparé, avec une petite pièce de quelques cases.

- **Sprites/textures réels pour bâtiments, PNJ et personnages** — aucun chargement de texture
  (`Texture2D`/fichier image) trouvé côté rendu du monde dans `Client/Program.cs` : toujours des
  silhouettes en couleurs unies + animations procédurales.
  **Proposition** : le plus gros chantier de contenu du projet, et un préalable à plusieurs
  autres idées ci-dessus. `Engine.Rendering.Texture2D` (StbImageSharp) est déjà prêt à charger
  des fichiers — ce qui manque, ce sont les assets eux-mêmes (à produire/acheter) et le
  remplacement des fonctions `DrawFigure`/silhouettes par un `SpriteBatch.Draw` texturé. À
  prioriser sur le personnage joueur et une espèce de monstre en premier, pour valider le pipeline
  avant de tout redessiner.

## Technique

- **Rafraîchissement en cours de session de la position des donjons** (il faut se reconnecter
  pour voir un changement d'heure survenu pendant que le client tourne).
  **Proposition** : le client sonde déjà `GET /api/dungeons` à la connexion — ajouter un sondage
  périodique léger (toutes les quelques minutes, pas 0,35s comme le combat) suffit, sans job
  serveur supplémentaire puisque `EnsureCurrentPosition` recalcule déjà paresseusement à la
  lecture.

- **Validation serveur de portée/collision sur les déplacements de joueurs en extérieur**.
  **Proposition** : vérifier côté serveur, à réception d'un `PlayerMove`, que la case ciblée est
  adjacente à la dernière position connue et n'est pas occupée par un bâtiment, avant de
  diffuser — rejeter silencieusement sinon plutôt que de faire confiance au client.

- **Authentification admin dédiée pour MonsterEditor/MapEditor** (outils internes toujours
  supposés lancés contre un serveur de confiance).
  **Proposition** : réutiliser `AdminAuthService` déjà écrit pour l'AdminPanel — un simple écran
  de connexion avant d'activer les boutons Créer/Modifier/Supprimer, pas un nouveau système.

- **Historique de tchat persisté entre connexions** (toujours borné à ~100 lignes en mémoire côté
  client, rien en base — pas d'entité `ChatMessage` dans `Database/Entities`).
  **Proposition** : nouvelle table `ChatMessageEntity` (canal, expéditeur, contenu, horodatage),
  purge automatique au-delà d'une rétention raisonnable (ex. 7 jours) pour ne pas grossir
  indéfiniment ; charger les 50 derniers messages du canal à l'ouverture du panneau Tchat.

- **Rendu visuel de la grille de donjon dans le MapEditor** (toujours une prévisualisation
  textuelle de la génération procédurale, pas de rendu graphique).
  **Proposition** : un simple `Canvas` WPF avec des rectangles colorés par type de salle suffit
  largement (pas besoin d'intégrer `Engine`/OpenGL dans WPF comme le redoutait déjà le README) —
  la disposition en grille existe déjà côté données (`DungeonFloorGenerator`), il ne manque que
  le dessin.

- **Déconnexion forcée immédiate d'un joueur banni/muet déjà connecté** (`PlayerSession.cs`) —
  l'effet ne s'applique aujourd'hui qu'au message suivant ou à la prochaine connexion.
  **Proposition** : uniquement pour le ban (le mute peut rester différé, c'est sans urgence) —
  garder une référence de session active par `UserId` (probablement déjà nécessaire pour la
  visibilité globale des joueurs) et fermer le socket dès qu'une action de bannissement le vise.

- **Vrai désinstallateur MSI** (l'installateur actuel s'auto-supprime via un `cmd.exe` détaché
  avec minuterie plutôt qu'un vrai désinstallateur).
  **Proposition** : migrer vers WiX Toolset pour produire un vrai `.msi`, ce qui donne aussi
  l'intégration standard "Programmes et fonctionnalités" gratuitement au lieu de la clé de
  registre manuelle actuelle — chantier à part entière, pas urgent tant que l'installateur
  maison fonctionne.

- **(2026-08-31) Ping Discord dédié à la rotation horaire des donjons** — `DigestScheduler` poste
  un récapitulatif horaire des changements de code ; rien n'annonce quels donjons sont actifs
  cette heure-ci, ni l'apparition d'un donjon invoqué par un admin (`DungeonAdminOverride`).
  **Proposition** : à la bascule d'heure, si les portails actifs ont changé, poster un petit embed
  « Donjons de l'heure : X (niv 1), Y (niv a-b) » via `DiscordAnnouncer` (même mécanisme que
  l'existant) — l'état est déjà calculé par `DungeonWorldService.GetActivePortals`, il suffit de
  comparer au tirage de l'heure précédente.

- **(2026-08-31) Garde-fou "déjà en combat" sur `spawn-encounter`** — `StartAdminEncounterAsync`
  (`H142`) appelle `StartAsync` sans vérifier si le personnage de l'admin a déjà une session de
  combat en cours (`CombatSessionStore`).
  **Proposition** : refuser avec un message clair si `combatStore.TryGetActiveByPartyId`/une
  recherche par `CharacterId` renvoie une session non terminée — même garde que celui déjà en
  place pour les combats de groupe, juste étendu à ce nouveau point d'entrée.

## UI / UX

- **Vraie image de profil** (toujours une pastille de couleur + initiale dérivées du pseudo,
  `Launcher/AvatarConverters.cs` — pas de pipeline d'upload/stockage d'image).
  **Proposition** : endpoint d'upload simple stockant l'image sur disque serveur (pas besoin de
  S3/CDN à cette échelle), taille limitée et redimensionnée côté serveur, référencée par une URL
  relative sur `UserEntity` — affichée en jeu et dans le Launcher/AdminPanel à la place de la
  pastille générée.

- **Suivi "tutoriel déjà vu" + affichage automatique à la toute première connexion** (F1 reste la
  seule façon de l'ouvrir).
  **Proposition** : un simple booléen `HasSeenTutorial` sur `CharacterEntity`, mis à `true` à la
  fermeture du tutoriel — ouverture automatique une seule fois juste après la création de
  personnage, F1 continue de fonctionner ensuite comme aujourd'hui.

- **Curseur de sélection quand plusieurs PNJ sont définis pour un même bâtiment** (un seul PNJ
  s'affiche aujourd'hui même si plusieurs sont configurés).
  **Proposition** : flèches gauche/droite pour faire défiler les PNJ du bâtiment quand il y en a
  plus d'un, indicateur "1/2" à l'écran — cohérent avec le style clic+clavier déjà utilisé
  ailleurs dans les panneaux.

- **Édition du type (Guerrier/Archer/Soigneur/...) d'une espèce depuis l'UI du MonsterEditor**
  (aujourd'hui modifiable seulement via l'API/le seeder).
  **Proposition** : un simple `ComboBox` lié au champ `Type` existant dans le formulaire d'édition
  d'espèce — l'endpoint `PUT /api/monsters/species` gère déjà ce champ, c'est purement un oubli
  d'UI plutôt qu'un vrai manque côté serveur.

- **(2026-08-31) Bâtiment « Miroir » pour re-personnaliser l'apparence** — la palette
  (peau/cheveux/vêtements/accessoire, `Client/World/CharacterAppearancePalette.cs`) est figée à la
  création du personnage (`CreateCharacterRequest`) ; aucun moyen de la changer ensuite.
  **Proposition** : un petit bâtiment « Miroir » (ou une option chez l'Aubergiste) rouvrant
  exactement l'UI de personnalisation de la création, contre un peu d'or. Nécessite juste de
  persister les champs d'apparence sur `CharacterEntity` (probablement déjà le cas) et un endpoint
  `POST /api/characters/{id}/appearance`.

- **(2026-08-31) Récapitulatif de combat détaillé sur l'écran de victoire** — l'écran de fin
  (`DrawCombat`) montre le résultat + le butin, mais aucune statistique de la rencontre. Les
  agrégats à vie existent (`CombatStatistics`) mais pas de vue par combat.
  **Proposition** : cumuler côté serveur, sur `CombatSession`, quelques compteurs simples pendant
  la résolution (dégâts infligés/subis par l'équipe du joueur, soins, nombre de tours) et les
  renvoyer dans `CombatSessionState` quand `IsFinished` ; le Client les affiche en 3-4 lignes
  sous la bannière VICTOIRE.

- **(2026-08-31) Journal de collection des variantes** — le jeu a 17 variantes de créature
  (`MonsterVariantCatalog`) mais rien ne trace lesquelles un joueur a déjà rencontrées ou
  capturées (façon « Pokédex des variantes »).
  **Proposition** : une table `SeenVariantEntity (CharacterId, SpeciesId, Variant, FirstSeenUtc)`
  alimentée à la capture (`CaptureService`) et au premier tour d'un combat sauvage
  (`CombatService`), affichée dans le panneau Monstres existant — une grille espèce × variante
  avec les cases obtenues en couleur. Donne un objectif de complétion long terme sans power creep.

## Autres

- **Notifications Discord** : catégories supplémentaires (annonce dédiée guerre de royaumes,
  ouverture de saison) en plus du récapitulatif quotidien et des annonces admin déjà en place.
  **Proposition** : brancher `DiscordAnnouncer` directement sur `KingdomWarService.ResolveAsync`
  (fin de guerre hebdo) et `SeasonService` (nouvelle saison) — même mécanisme d'embed que
  l'existant, juste deux nouveaux points d'appel.

- **Système de réputation/grade militaire pour le PvP sauvage** (mentionné au GDD, aucune trace
  dans le code) et **zones à risque hors arène** elles-mêmes, qui semblent aussi absentes.
  **Proposition** : gros morceau de gameplay, à cadrer avant de coder — commencer par les zones à
  risque (une portion de `WorldMap` en dehors des capitales où le combat PvP direct est autorisé
  sans passer par l'arène) avant même de penser au système de réputation qui en découle.

- **Récompenses cosmétiques exclusives au-delà de "Rare" dans le Passe de Niveau premium**
  (plafonné volontairement à Rare, voir `Server/World/BattlePassService.cs`).
  **Proposition** : ajouter un ou deux paliers "cosmétique unique" en fin de piste premium
  (titre exclusif, teinte de personnage) plutôt que de monter la rareté des objets eux-mêmes —
  évite le déséquilibre pay-to-win tout en donnant une vraie exclusivité au premium.

- **(2026-08-31) Historique des donjons de l'heure précédente consultable en jeu** — les portails
  changent chaque heure (`H142`) et un joueur qui se connecte juste après une rotation ne sait
  pas quel donjon vient de disparaître.
  **Proposition** : `GET /api/dungeons/active` renvoie aussi les portails de l'heure UTC
  précédente (même calcul `GetActivePortals` avec `hourBucket - 1`, aucun stockage) ; le Client
  les affiche en grisé dans une petite ligne « Heure précédente : … » sous les portails actuels.

- **(2026-08-31) Filtrer les commandes du panneau admin** — `AdminPanelCommands()` renvoie
  jusqu'à ~25 entrées (dont 3 ajoutées en `H142`) ; retrouver une commande précise demande de
  faire défiler toute la liste.
  **Proposition** : un champ de recherche en haut du panneau qui filtre la liste par sous-chaîne
  du libellé (insensible casse/accents, `MonsterVariantCatalog`-style) — purement côté Client,
  la logique de dispatch par index/libellé de `SubmitAdminPanelCommand` reste inchangée.
