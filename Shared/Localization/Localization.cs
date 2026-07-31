using System.Linq;
using Aetheria.Shared.Settings;

namespace Aetheria.Shared.Localization;

/// <summary>
/// Voir GDD/demande utilisateur — "ajoute un parametre pour changer la langue... traduit tout
/// les dialogue". Le français reste la langue source : chaque chaîne statique affichée dans le
/// jeu peut être cherchée ici pour obtenir sa traduction anglaise. Les chaînes absentes du
/// dictionnaire (notamment celles construites par interpolation avec du contenu dynamique — nom
/// de joueur, nombres, etc., voir Client/Program.cs) retombent silencieusement sur le français
/// plutôt que d'afficher un texte manquant — couverture partielle mais fonctionnelle, voir
/// Client/Program.cs (DrawText/DrawTextCentered/MeasureTextWidth) pour comment ce dictionnaire
/// est branché. Le japonais (voir <see cref="Language.Japonais"/>) n'a volontairement aucune
/// entrée : reste désactivé côté UI tant qu'aucun vrai système de police ne supporte les glyphes
/// japonais (voir retour utilisateur explicite à ce sujet).
/// </summary>
public static class Localization
{
    public static string Translate(string text, Language language)
    {
        if (language != Language.Anglais)
        {
            return text;
        }

        if (English.TryGetValue(text, out var exact))
        {
            return exact;
        }

        // Voir retour utilisateur — "quand on choisis anglais il n'y a quasiment rien en
        // anglais, remplace tout les texte" : beaucoup de textes du jeu sont construits par
        // interpolation ($"...{valeurDynamique}...") — leur forme finale ne correspond jamais
        // exactement à une entrée du dictionnaire ci-dessus (voir Client/Program.cs). Repli sur
        // un remplacement de sous-chaînes : chaque fragment français statique connu (voir
        // EnglishFragments, longueur décroissante pour ne pas laisser un fragment court amputer
        // un fragment plus long qui le contient) est remplacé où qu'il apparaisse dans le texte,
        // le contenu dynamique entre les fragments reste inchangé.
        var result = text;
        foreach (var fragment in FragmentKeysByDescendingLength)
        {
            if (result.Contains(fragment, StringComparison.Ordinal))
            {
                result = result.Replace(fragment, EnglishFragments[fragment]);
            }
        }

        return result;
    }

    private static readonly Dictionary<string, string> English = new()
    {
        // Panneaux / titres.
        ["PARAMETRES"] = "SETTINGS",
        ["AMIS"] = "FRIENDS",
        ["ARENE CLASSEE"] = "RANKED ARENA",
        ["BOSS MONDIAL"] = "WORLD BOSS",
        ["BOUTIQUE GEMMES"] = "GEM SHOP",
        ["CLASSEMENT"] = "LEADERBOARD",
        ["CLASSEMENT DES GUILDES"] = "GUILD LEADERBOARD",
        ["CLASSEMENT DES ROYAUMES"] = "KINGDOM LEADERBOARD",
        ["COFFRE PARTAGE"] = "SHARED CHEST",
        ["DEFIS"] = "CHALLENGES",
        ["DONJONS"] = "DUNGEONS",
        ["ENCYCLOPEDIE"] = "ENCYCLOPEDIA",
        ["FUSION"] = "FUSION",
        ["GROUPE"] = "PARTY",
        ["GUERRE DE GUILDES"] = "GUILD WAR",
        ["GUERRE DE ROYAUMES"] = "KINGDOM WAR",
        ["GUILDE"] = "GUILD",
        ["INVENTAIRE"] = "INVENTORY",
        ["METIERS"] = "PROFESSIONS",
        ["MONSTRES"] = "MONSTERS",
        ["MONTURES"] = "MOUNTS",
        ["OPTIONS"] = "OPTIONS",
        ["PANEL ADMIN"] = "ADMIN PANEL",
        ["PASSE DE NIVEAU"] = "BATTLE PASS",
        ["PROFIL"] = "PROFILE",
        ["REPRODUCTION"] = "BREEDING",
        ["ROYAUME"] = "KINGDOM",
        ["ROYAUMES"] = "KINGDOMS",
        ["SIGNALEMENTS"] = "REPORTS",
        ["TCHAT"] = "CHAT",
        ["TELEPORTEUR"] = "TELEPORTER",

        // Messages "vide"/statut.
        ["AUCUN BOSS MONDIAL ACTIF POUR LE MOMENT"] = "NO WORLD BOSS ACTIVE RIGHT NOW",
        ["AUCUN DEFI DISPONIBLE"] = "NO CHALLENGE AVAILABLE",
        ["AUCUN DONJON DANS CE ROYAUME"] = "NO DUNGEON IN THIS KINGDOM",
        ["AUCUN OBJET RECLAME"] = "NO ITEM CLAIMED",
        ["AUCUN SIGNALEMENT"] = "NO REPORTS",
        ["AUCUNE ANNONCE POUR L'INSTANT"] = "NO ANNOUNCEMENT RIGHT NOW",
        ["AUCUNE ARME/ARMURE/ACCESSOIRE EN INVENTAIRE"] = "NO WEAPON/ARMOR/ACCESSORY IN INVENTORY",
        ["AUCUNE CREATURE POUR L'INSTANT"] = "NO CREATURE YET",
        ["AUCUNE DONNEE POUR CE CLASSEMENT"] = "NO DATA FOR THIS LEADERBOARD",
        ["AUCUNE GUILDE TROUVEE"] = "NO GUILD FOUND",
        ["AUCUNE QUETE EN COURS"] = "NO QUEST IN PROGRESS",
        ["Aucun rang PvP enregistre pour le moment."] = "No PvP rank recorded yet.",
        ["Aucune donnée pour ce classement."] = "No data for this leaderboard.",
        ["Aucun ami pour l'instant."] = "No friends yet.",
        ["BIENTOT DISPONIBLE"] = "COMING SOON",
        ["CHARGEMENT DU COMBAT..."] = "LOADING COMBAT...",
        ["CHARGEMENT..."] = "LOADING...",
        ["Chargement..."] = "Loading...",
        ["CONNEXION EN COURS..."] = "CONNECTING...",
        ["IL FAUT AU MOINS 2 CREATURES"] = "AT LEAST 2 CREATURES REQUIRED",
        ["INVENTAIRE VIDE"] = "EMPTY INVENTORY",
        ["LE COFFRE EST VIDE"] = "THE CHEST IS EMPTY",
        ["PRET"] = "READY",
        ["PRET - RECHERCHE D'UNE GUILDE ADVERSE..."] = "READY - SEARCHING FOR AN OPPONENT GUILD...",
        ["PRET A NAITRE ! (ENTREE POUR RECUPERER)"] = "READY TO HATCH! (ENTER TO COLLECT)",
        ["Pass Premium déjà actif — merci !"] = "Premium Pass already active — thank you!",
        ["QUETES EN COURS"] = "QUESTS IN PROGRESS",
        ["RECHERCHE D'ADVERSAIRES..."] = "SEARCHING FOR OPPONENTS...",
        ["RECHERCHE D'UN ADVERSAIRE..."] = "SEARCHING FOR AN OPPONENT...",
        ["TOUR ADVERSE..."] = "OPPONENT'S TURN...",
        ["UN VIEUX GARDIEN VOUS ACCUEILLE"] = "AN OLD GUARDIAN WELCOMES YOU",
        ["VOUS N'APPARTENEZ A AUCUNE GUILDE"] = "YOU DO NOT BELONG TO ANY GUILD",
        ["VOUS N'ETES DANS AUCUN GROUPE"] = "YOU ARE NOT IN ANY PARTY",
        ["FUSION PRETE ! (ENTREE POUR RECUPERER)"] = "FUSION READY! (ENTER TO COLLECT)",
        ["LES DEUX PARENTS SURVIVENT"] = "BOTH PARENTS SURVIVE",
        ["LE FORGERON PROPOSE :"] = "THE BLACKSMITH OFFERS:",
        ["QUITTER LE DONJON ?"] = "LEAVE THE DUNGEON?",
        ["CONFIRMER LA FUSION ?"] = "CONFIRM FUSION?",
        ["CONFIRMER LA REPRODUCTION ?"] = "CONFIRM BREEDING?",

        // Écrans de connexion / création de personnage.
        ["+ NOUVEAU PERSONNAGE"] = "+ NEW CHARACTER",
        ["CHOISIS TON COMPAGNON"] = "CHOOSE YOUR COMPANION",
        ["CHOISIS TON PERSONNAGE"] = "CHOOSE YOUR CHARACTER",
        ["PERSONNALISE TON APPARENCE"] = "CUSTOMIZE YOUR APPEARANCE",
        ["QUEL EST TON NOM ?"] = "WHAT IS YOUR NAME?",
        ["TAPEZ VOTRE NOM (3 LETTRES MIN.) - ENTREE POUR CONTINUER"] = "TYPE YOUR NAME (3 LETTERS MIN.) - ENTER TO CONTINUE",

        // Prompts / raccourcis (bas d'écran).
        ["APPUYEZ SUR E POUR PARLER"] = "PRESS E TO TALK",
        ["APPUYEZ SUR ECHAP POUR SORTIR"] = "PRESS ESCAPE TO EXIT",
        ["APPUYEZ SUR ENTREE"] = "PRESS ENTER",
        ["C : CREER UNE GUILDE"] = "C: CREATE A GUILD",
        ["CLIC OU ENTREE : VALIDER - TAB : ACHAT/VENTE - ECHAP : FERMER"] = "CLICK OR ENTER: CONFIRM - TAB: BUY/SELL - ESCAPE: CLOSE",
        ["CLIC OU ENTREE : VOYAGER - HAUT/BAS : CHOISIR - ECHAP : ANNULER"] = "CLICK OR ENTER: TRAVEL - UP/DOWN: CHOOSE - ESCAPE: CANCEL",
        ["CODE D'INVITATION (5 CHIFFRES) :"] = "INVITE CODE (5 DIGITS):",
        ["CODE DU GROUPE A REJOINDRE (5 CHIFFRES) :"] = "PARTY CODE TO JOIN (5 DIGITS):",
        ["D : BANQUE   K : COFFRE   L : CLASSEMENT   W : GUERRE"] = "D: BANK   K: CHEST   L: LEADERBOARD   W: WAR",
        ["D : description - GAUCHE/DROITE : titre - HAUT/BAS : monture - TAB : ailes - ECHAP : fermer"] = "D: description - LEFT/RIGHT: title - UP/DOWN: mount - TAB: wings - ESCAPE: close",
        ["DEFIER EN DUEL"] = "CHALLENGE TO DUEL",
        ["ECHAP : FERMER"] = "ESCAPE: CLOSE",
        ["ECHAP : fermer"] = "Escape: close",
        ["ECHAP POUR ANNULER"] = "ESCAPE TO CANCEL",
        ["ECHAP POUR FERMER"] = "ESCAPE TO CLOSE",
        ["ECHAP POUR REVENIR"] = "ESCAPE TO GO BACK",
        ["EN LIGNE"] = "ONLINE",
        ["ENTREE : CONFIRMER - ECHAP : RETOUR"] = "ENTER: CONFIRM - ESCAPE: BACK",
        ["ENTREE : CREER UN GROUPE"] = "ENTER: CREATE A PARTY",
        ["ENTREE : DEFIER - ECHAP : ANNULER"] = "ENTER: CHALLENGE - ESCAPE: CANCEL",
        ["ENTREE : ENVOYER - ECHAP : ANNULER"] = "ENTER: SEND - ESCAPE: CANCEL",
        ["ENTREE : EQUIPER - ECHAP : ANNULER"] = "ENTER: EQUIP - ESCAPE: CANCEL",
        ["ENTREE : REJOINDRE - ECHAP : NOUVELLE RECHERCHE"] = "ENTER: JOIN - ESCAPE: NEW SEARCH",
        ["ENTREE : VALIDER - ECHAP : ANNULER"] = "ENTER: CONFIRM - ESCAPE: CANCEL",
        ["ENTREE : VALIDER - ECHAP : ANNULER LA SAISIE"] = "ENTER: CONFIRM - ESCAPE: CANCEL INPUT",
        ["ENTREE OU CLIC : EPINGLER/DESEPINGLER - ECHAP : FERMER"] = "ENTER OR CLICK: PIN/UNPIN - ESCAPE: CLOSE",
        ["ENTREE POUR CONTINUER"] = "ENTER TO CONTINUE",
        ["ENTREE POUR RECHERCHER - ECHAP POUR ANNULER"] = "ENTER TO SEARCH - ESCAPE TO CANCEL",
        ["ENTREE POUR REJOINDRE - ECHAP POUR ANNULER"] = "ENTER TO JOIN - ESCAPE TO CANCEL",
        ["ENTREE POUR VALIDER - ECHAP POUR ANNULER"] = "ENTER TO CONFIRM - ESCAPE TO CANCEL",
        ["F9 POUR CHANGER"] = "F9 TO CHANGE",
        ["FLECHES : CHOISIR - ENTREE : REJOINDRE LA FILE"] = "ARROWS: CHOOSE - ENTER: JOIN QUEUE",
        ["FLECHES : NAVIGUER - ECHAP OU F1 : FERMER"] = "ARROWS: NAVIGATE - ESCAPE OR F1: CLOSE",
        ["FLECHES OU CLIC : CHOISIR - ENTREE : RECLAMER"] = "ARROWS OR CLICK: CHOOSE - ENTER: CLAIM",
        ["FLECHES POUR CHOISIR - ENTREE POUR VALIDER"] = "ARROWS TO CHOOSE - ENTER TO CONFIRM",
        ["GAUCHE/DROITE : categorie - ECHAP : fermer"] = "LEFT/RIGHT: category - ESCAPE: close",
        ["GAUCHE/DROITE : classement - ENTREE : attaquer - ECHAP : fermer"] = "LEFT/RIGHT: leaderboard - ENTER: attack - ESCAPE: close",
        ["HAUT/BAS : CHAMP - GAUCHE/DROITE : VALEUR - ENTREE : CONTINUER"] = "UP/DOWN: FIELD - LEFT/RIGHT: VALUE - ENTER: CONTINUE",
        ["HAUT/BAS : CHOISIR - ENTREE : VALIDER - ECHAP : FERMER (F2)"] = "UP/DOWN: CHOOSE - ENTER: CONFIRM - ESCAPE: CLOSE (F2)",
        ["HAUT/BAS : DEFILER - ECHAP : FERMER"] = "UP/DOWN: SCROLL - ESCAPE: CLOSE",
        ["HAUT/BAS : ROYAUME - ENTREE : CONTINUER"] = "UP/DOWN: KINGDOM - ENTER: CONTINUE",
        ["HAUT/BAS : choisir - C OU CLIC : fabriquer - ECHAP : fermer"] = "UP/DOWN: choose - C OR CLICK: craft - ESCAPE: close",
        ["HAUT/BAS : choisir - ENTREE : MP/accepter - SUPPR : retirer - A : ajouter - ECHAP : fermer"] = "UP/DOWN: choose - ENTER: whisper/accept - DEL: remove - A: add - ESCAPE: close",
        ["HAUT/BAS : choisir - ENTREE : reclamer - ECHAP : fermer"] = "UP/DOWN: choose - ENTER: claim - ESCAPE: close",
        ["HAUT/BAS : choisir - TAB : hardcore/normal - ENTREE : entrer - ECHAP : fermer"] = "UP/DOWN: choose - TAB: hardcore/normal - ENTER: enter - ESCAPE: close",
        ["HAUT/BAS : parcourir - ECHAP : FERMER"] = "UP/DOWN: browse - ESCAPE: CLOSE",
        ["HAUT/BAS : parcourir - TAB : especes/montures - ECHAP : fermer"] = "UP/DOWN: browse - TAB: species/mounts - ESCAPE: close",
        ["HAUT/BAS : parcourir la route - ECHAP : FERMER"] = "UP/DOWN: browse the track - ESCAPE: CLOSE",
        ["I : DEPOSER 1ER OBJET DE L'INVENTAIRE - O : RETIRER LA SELECTION"] = "I: DEPOSIT 1ST INVENTORY ITEM - O: WITHDRAW SELECTION",
        ["J : REJOINDRE PAR IDENTIFIANT"] = "J: JOIN BY ID",
        ["L : QUITTER LE GROUPE"] = "L: LEAVE PARTY",
        ["MEILLEURS JOUEURS DE VOTRE ROYAUME (PVP)"] = "TOP PLAYERS OF YOUR KINGDOM (PVP)",
        ["MONTANT A DEPOSER A LA BANQUE :"] = "AMOUNT TO DEPOSIT AT THE BANK:",
        ["NOM A RECHERCHER (VIDE = TOUTES) :"] = "NAME TO SEARCH (EMPTY = ALL):",
        ["NOM DE LA NOUVELLE GUILDE :"] = "NAME OF THE NEW GUILD:",
        ["NOM DU CANDIDAT (VOTRE ROYAUME) :"] = "CANDIDATE NAME (YOUR KINGDOM):",
        ["R : RECHERCHER / REJOINDRE UNE GUILDE"] = "R: SEARCH / JOIN A GUILD",
        ["R OU CLIC : RECOLTER - ECHAP : FERMER"] = "R OR CLICK: GATHER - ESCAPE: CLOSE",
        ["T : TP VERS LE RAPPORTEUR - Y : TP VERS LE SIGNALE - ENTREE : MARQUER TRAITE - ECHAP : FERMER"] = "T: TP TO REPORTER - Y: TP TO REPORTED - ENTER: MARK RESOLVED - ESCAPE: CLOSE",
        ["TAB : CANAL - ENTREE : ENVOYER - ECHAP : FERMER"] = "TAB: CHANNEL - ENTER: SEND - ESCAPE: CLOSE",
        ["BUTIN - CHOISISSEZ UN OBJET"] = "LOOT - CHOOSE AN ITEM",
        ["Une victoire rapporte des points de guerre a votre guilde."] = "A victory earns war points for your guild.",
        ["Une victoire rapporte des points de guerre a votre royaume."] = "A victory earns war points for your kingdom.",
        ["Affrontez un joueur d'un autre royaume."] = "Fight a player from another kingdom.",
        ["Affrontez un membre d'une autre guilde en duel amical."] = "Fight a member of another guild in a friendly duel.",

        // Amis / guilde / bannissement / divers (DrawText non centré).
        ["AMIS :"] = "FRIENDS:",
        ["Acheter des gemmes avec de l'argent reel :"] = "Buy gems with real money:",
        ["DEMANDES RECUES :"] = "RECEIVED REQUESTS:",
        ["Description (200 caracteres max) :"] = "Description (200 characters max):",
        ["Description :"] = "Description:",
        ["ECHAP : QUITTER LE DONJON - ZQSD/FLECHES : SE DEPLACER"] = "ESCAPE: LEAVE DUNGEON - WASD/ARROWS: MOVE",
        ["GRATUIT"] = "FREE",
        ["HEBDOMADAIRES"] = "WEEKLY",
        ["MEMBRES :"] = "MEMBERS:",
        ["MENSUELS"] = "MONTHLY",
        ["Nom du personnage a ajouter :"] = "Name of the character to add:",
        ["PALIER"] = "TIER",
        ["PREMIUM"] = "PREMIUM",
        ["Pseudo du joueur a defier :"] = "Username of the player to challenge:",
        ["Si son groupe (ou le votre) compte plusieurs joueurs,"] = "If their party (or yours) has multiple players,",
        ["tous ses membres devront accepter pour lancer le combat."] = "all its members will need to accept to start the fight.",

        // Tutoriel (voir TutorialPages) : titres.
        ["BIENVENUE DANS AETHERIA"] = "WELCOME TO AETHERIA",
        ["SE DEPLACER"] = "MOVING AROUND",
        ["INTERAGIR"] = "INTERACTING",
        ["PANNEAUX EN JEU"] = "IN-GAME PANELS",
        ["COMBAT"] = "COMBAT",
        ["TYPES ELEMENTAIRES"] = "ELEMENT TYPES",

        // Tutoriel : lignes purement informatives (Action vide).
        ["Ce tutoriel explique les bases du jeu."] = "This tutorial explains the basics of the game.",
        ["Approchez-vous d'un PNJ, d'un bâtiment ou d'un donjon :"] = "Approach an NPC, a building or a dungeon:",
        ["un message apparaît en bas de l'écran."] = "a message appears at the bottom of the screen.",
        ["Boutique, Hôtel des ventes, Forge, Mine, Pension et"] = "Shop, Auction House, Forge, Mine, Inn and",
        ["Téléporteur s'ouvrent en visitant leur bâtiment en ville."] = "Teleporter open by visiting their building in town.",
        ["Un donjon apparaît à un endroit aléatoire de la carte"] = "A dungeon appears at a random spot on the map",
        ["et change de position toutes les heures."] = "and changes position every hour.",
        ["(combats, coffres d'or, et autres événements)."] = "(fights, gold chests, and other events).",
        ["Feu > Nature, Glace   Eau > Feu, Terre"] = "Fire > Nature, Ice   Water > Fire, Earth",
        ["Nature > Eau, Terre   Glace > Nature, Air"] = "Nature > Water, Earth   Ice > Nature, Air",
        ["Foudre > Eau, Air     Terre > Foudre, Feu"] = "Lightning > Water, Air   Earth > Lightning, Fire",
        ["Air > Terre, Nature   Lumière > Ombre"] = "Air > Earth, Nature   Light > Shadow",
        ["Ombre > Lumière       Neutre : sans avantage"] = "Shadow > Light   Neutral: no advantage",
        ["'>' = 1.5x dégâts infligés, 0.67x dégâts subis en retour."] = "'>' = 1.5x damage dealt, 0.67x damage taken in return.",

        // Tutoriel : touches d'action (voir "[{step.Action}]" dans DrawTutorialOverlay).
        ["[FLECHES G/D OU ENTREE]"] = "[ARROWS L/R OR ENTER]",
        ["[ECHAP]"] = "[ESCAPE]",
        ["[W A S D (OU Z Q S D)]"] = "[W A S D]",
        ["[CLIC SUR LA CARTE]"] = "[CLICK THE MAP]",
        ["[F9]"] = "[F9]",
        ["[E]"] = "[E]",
        ["[I]"] = "[I]",
        ["[M]"] = "[M]",
        ["[P]"] = "[P]",
        ["[G]"] = "[G]",
        ["[V]"] = "[V]",
        ["[1]"] = "[1]",
        ["[2]"] = "[2]",
        ["[3]"] = "[3]",
        ["[4]"] = "[4]",
        ["[FLECHES + ENTREE OU CLIC]"] = "[ARROWS + ENTER OR CLICK]",
        ["[ENTREE]"] = "[ENTER]",

        // Tutoriel : effets d'action (voir " {step.Effect}" dans DrawTutorialOverlay — espace initial inclus).
        [" avancer dans ce tutoriel."] = " to move through this tutorial.",
        [" fermer ce tutoriel (F1 pour le rouvrir à tout moment)."] = " to close this tutorial (F1 to reopen it any time).",
        [" se déplacer sur la carte."] = " to move around the map.",
        [" tracer un chemin jusqu'à la case cliquée."] = " to trace a path to the clicked tile.",
        [" changer la disposition clavier détectée."] = " to change the detected keyboard layout.",
        [" parler au PNJ ou entrer dans le bâtiment/donjon."] = " to talk to the NPC or enter the building/dungeon.",
        [" ouvrir l'Inventaire."] = " to open the Inventory.",
        [" ouvrir la liste de vos Monstres."] = " to open your Monster list.",
        [" ouvrir le Groupe."] = " to open the Party.",
        [" ouvrir la Guilde."] = " to open the Guild.",
        [" ouvrir l'Arène classée."] = " to open the Ranked Arena.",
        [" Déplacer votre créature sur la grille."] = " Move your creature on the grid.",
        [" Attaquer une cible à portée."] = " Attack a target in range.",
        [" Passer votre tour."] = " Pass your turn.",
        [" Capturer (nécessite une Carte de capture)."] = " Capture (requires a Capture Card).",
        [" viser une case en surbrillance."] = " to aim at a highlighted tile.",
        [" avancer de salle en salle une fois la salle nettoyée."] = " to move room to room once the room is cleared.",

        // Panneau Paramètres (voir DrawSettingsPanel).
        ["HAUT/BAS : CHOISIR - GAUCHE/DROITE : CHANGER - ECHAP : FERMER"] = "UP/DOWN: CHOOSE - LEFT/RIGHT: CHANGE - ESCAPE: CLOSE",
        ["JAPONAIS : BIENTOT (police non supportee)"] = "JAPANESE: COMING SOON (font not supported)",

        // Complément (voir retour utilisateur — "quasiment rien en anglais, traduit tout").
        ["ECHAP : ANNULER"] = "ESCAPE: CANCEL",
        ["ENTREE : PRET - ECHAP : FERMER"] = "ENTER: READY - ESCAPE: CLOSE",
        ["ESPECE INCONNUE"] = "UNKNOWN SPECIES",
        ["BOUTIQUE - VENTE"] = "SHOP - SELL",
        ["BOUTIQUE - ACHAT"] = "SHOP - BUY",
        ["HOTEL DES VENTES - DEPOSER"] = "AUCTION HOUSE - DEPOSIT",
        ["HOTEL DES VENTES"] = "AUCTION HOUSE",
        ["APPUYEZ SUR E POUR CONTINUER"] = "PRESS E TO CONTINUE",
        ["APPUYEZ SUR E POUR FERMER"] = "PRESS E TO CLOSE",
        ["APPUYEZ SUR ENTREE POUR AFFRONTER UN MONSTRE SAUVAGE"] = "PRESS ENTER TO FIGHT A WILD MONSTER",
        ["VICTOIRE !"] = "VICTORY!",
        ["DEFAITE..."] = "DEFEAT...",
        ["CHOISISSEZ LA PREMIERE CREATURE (ENTREE)"] = "CHOOSE THE FIRST CREATURE (ENTER)",
        ["CHOISISSEZ LE PREMIER PARENT (ENTREE)"] = "CHOOSE THE FIRST PARENT (ENTER)",
        ["CONTENU END-GAME DEBLOQUE (donjon mythique Sanctuaire Ultime)"] = "END-GAME CONTENT UNLOCKED (Ultimate Sanctuary mythic dungeon)",
        ["Grade au palier maximum (Légende)."] = "Grade at maximum tier (Legend).",
        ["APPUYEZ SUR E POUR ENTRER DANS LE DONJON"] = "PRESS E TO ENTER THE DUNGEON",
        ["ECHAP : RETOUR"] = "ESCAPE: BACK",
        ["P : PRESTIGE (reinitialise le niveau, +5% de stats permanent) - ECHAP : RETOUR"] = "P: PRESTIGE (resets level, +5% permanent stats) - ESCAPE: BACK",
    };

    /// <summary>
    /// Voir GDD/demande utilisateur — couverture des textes construits par interpolation (voir
    /// <see cref="Translate"/>) : fragments français statiques extraits des chaînes $"..." du
    /// client (portions entre/autour des {valeurs dynamiques}), avec leur traduction anglaise.
    /// Volontairement limité aux fragments d'au moins quelques caractères significatifs — les
    /// fragments trop courts/génériques (ponctuation seule, une lettre...) sont exclus : un
    /// remplacement de sous-chaîne sur un fragment aussi court que "s" ou ")" corromprait la
    /// quasi-totalité du texte du jeu (voir Translate, Contains/Replace).
    /// </summary>
    private static readonly Dictionary<string, string> EnglishFragments = new()
    {
        [" (NIV. "] = " (LVL. ",
        [" (Nv."] = " (Lvl.",
        [" (TAB POUR ANNULER)"] = " (TAB TO CANCEL)",
        [" - NIV. "] = " - LVL. ",
        [" - Nv."] = " - Lvl.",
        [" : SE DEPLACER - F9 : CLAVIER - F1 : AIDE"] = ": MOVE - F9: KEYBOARD - F1: HELP",
        [" ESPECES DECOUVERTES"] = " SPECIES DISCOVERED",
        [" EST EN INCUBATION"] = " IS INCUBATING",
        [" FUSIONNE AVEC "] = " FUSED WITH ",
        [" JOUEURS"] = " PLAYERS",
        [" MEMBRES)"] = " MEMBERS)",
        [" MONTURES POSSEDEES"] = " MOUNTS OWNED",
        [" OR (GAUCHE/DROITE POUR AJUSTER)"] = " GOLD (LEFT/RIGHT TO ADJUST)",
        [" POINTS DE GUERRE CETTE SEMAINE"] = " WAR POINTS THIS WEEK",
        [" SERA CONSOMMEE"] = " WILL BE CONSUMED",
        [" SURVIVRA (NIV. "] = " WILL SURVIVE (LVL. ",
        [" VOUS DEFIE EN DUEL !"] = " CHALLENGES YOU TO A DUEL!",
        [" XP DE GUILDE"] = " GUILD XP",
        [" degats"] = " damage",
        [" joueurs) doit accepter."] = " players) must accept.",
        [" possedee(s))"] = " owned)",
        ["Ailes actives : "] = "Active wings: ",
        ["CONTROLE PAR : "] = "CONTROLLED BY: ",
        ["CONTROLEE PAR : "] = "CONTROLLED BY: ",
        ["EN COURS... ("] = "IN PROGRESS... (",
        ["EN FILE : "] = "IN QUEUE: ",
        ["ENCHERE ACTUELLE : "] = "CURRENT BID: ",
        ["ENCHERIR SUR "] = "BID ON ",
        ["EQUIPER "] = "EQUIP ",
        ["ETAGE "] = "FLOOR ",
        ["GUILDE PRIVEE - CODE D'INVITATION : "] = "PRIVATE GUILD - INVITE CODE: ",
        ["Gemmes : "] = "Gems: ",
        ["MESSAGE PRIVE A "] = "PRIVATE MESSAGE TO ",
        ["Monture active : "] = "Active mount: ",
        ["NIVEAU FINAL : "] = "FINAL LEVEL: ",
        ["NIVEAU "] = "LEVEL ",
        ["Niveau "] = "Level ",
        ["Or : "] = "Gold: ",
        ["RAISON : "] = "REASON: ",
        ["ROYAUME VAINQUEUR : "] = "WINNING KINGDOM: ",
        ["S RESTANT)"] = "S LEFT)",
        ["SALLES NETTOYEES : "] = "ROOMS CLEARED: ",
        ["VAINCU PAR "] = "DEFEATED BY ",
        ["VOTRE OFFRE : "] = "YOUR BID: ",
        ["Votre groupe entier ("] = "Your entire party (",
        [" PV"] = " HP",
        [" OR"] = " GOLD",
        ["CODE : "] = "CODE: ",
        ["Discord : "] = "Discord: ",
        ["Twitch : "] = "Twitch: ",
        ["YouTube : "] = "YouTube: ",
        ["TAB : "] = "TAB: ",
        ["PAGE "] = "PAGE ",
        ["VERSION "] = "VERSION ",

        // Complément (voir retour utilisateur — "quasiment rien en anglais, traduit tout").
        ["PREMIERE : "] = "FIRST: ",
        [" - CHOISISSEZ LA SECONDE"] = " - CHOOSE THE SECOND",
        ["PREMIER PARENT : "] = "FIRST PARENT: ",
        [" - CHOISISSEZ LE SECOND"] = " - CHOOSE THE SECOND",
        ["END-GAME : "] = "END-GAME: ",
        [" especes niv. max - "] = " max-level species - ",
        [" succes"] = " achievements",
        ["[G] Passer "] = "[G] Move to ",
        [" gemmes"] = " gems",
        ["APPUYEZ SUR E POUR PARLER A "] = "PRESS E TO TALK TO ",
        ["APPUYEZ SUR E POUR ENTRER : "] = "PRESS E TO ENTER: ",
        ["Tour de "] = "Turn: ",
        [" (vous)"] = " (you)",
        ["DISPOSITION CLAVIER : "] = "KEYBOARD LAYOUT: ",
    };

    private static readonly List<string> FragmentKeysByDescendingLength =
        [.. EnglishFragments.Keys.OrderByDescending(k => k.Length)];
}
