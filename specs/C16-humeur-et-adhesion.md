# Humeur, pensées et adhésion
Statut : validé (go de Guillaume le 2026-10-30)
Créé : 2026-10-30

## But
C'est le chantier qui ferme la boucle. Les golfeurs réagissent au parcours, et le joueur lit leurs réactions pour corriger ses trous. Les visiteurs contents deviennent membres et la ligue recrute parmi eux. Le chantier couvre les manques M4 et M5 de `docs/plan-alpha.md`. À la fin de C16 a lieu la première vraie séance de jeu.

## Périmètre et hors périmètre
Dans le périmètre :
- l'humeur et une quinzaine d'événements ;
- l'abandon ;
- les pensées en bulle, l'info-bulle et la fiche du golfeur ;
- le comptage des réactions par trou et sa carte ;
- l'adhésion à quatre niveaux, avec démission ;
- la ligue branchée sur les vrais membres ;
- le pro qui se joint à un groupe de la file (reporté de C15, décision 20).

Hors périmètre :
- les besoins (faim, soif, fatigue) et les bâtiments qui donnent de l'humeur (C17) ;
- les effets directs de la météo sur l'humeur (choix 6) ;
- les VIP ;
- le plafond de membres (C17) ;
- les avis et la réputation par public ;
- le tutoriel qui lit une pensée (C19) ;
- les animations de réaction (C23).

## Conception
- **Humeur** (`Golfers/Mood.cs`, sim pure) : un entier de −10 à +10 par golfeur, qui entre dans `Checksum` et `Save` (v13 → v14).
  - L'humeur de départ et la baisse après chaque trou sont en données (`data/golfers/mood.json`), réglées pour « Facile ».
  - Sous zéro, le golfeur **abandonne** : il quitte la partie, rentre au pavillon par le champ de flux de C15, et lève l'évènement `GolferQuit` avec sa raison. Ses partenaires perdent 1 (choix 7). Un membre qui abandonne démissionne.
  - Le green fee du trou suit l'humeur (facteur de ×0,5 à ×1,2, déjà en données dans C14).
- **Événements** (`Golfers/MoodEvents.cs`) : chaque événement a un identifiant stable, une valeur en données et un texte de pensée en français.
  - Ceux que la sim mesure déjà :
    - putt difficile réussi ;
    - putt facile raté ;
    - coup décevant ;
    - balle dans l'eau ;
    - balle en bunker ;
    - balle contre un arbre ;
    - pas d'endroit sûr où viser (planificateur) ;
    - attente au départ ;
    - jeu lent ;
    - presque touché par une balle ;
    - vue panoramique (`BeautyMap`) ;
    - gazon abîmé (usure de `TurfGrid`) ;
    - arbre remarquable ;
    - beau trou ;
    - marche longue ;
    - le pro joue avec nous (choix 5).
  - Une contrariété mineure ne compte qu'à sa répétition, et seuil et répétition sont en données.
  - Pas d'événement de météo (choix 6) : la pluie et le vent agissent déjà par la difficulté des coups.
  - Chaque événement lève `MoodChanged(golfer, event, hole, cell)`.
- **Réactions par trou** (`Golfers/Reactions.cs`) : des compteurs par trou et par événement, dans `Checksum` et `Save`, remis à zéro chaque mois et gardés pour le mois précédent. La réputation de C15 prend l'humeur de sortie à la place de « finie = 1 ».
- **Adhésion** (`Golfers/Membership.cs`) :
  - Visitor, Member, Silver, Gold, Platinum.
  - Après une partie, le golfeur gagne des points selon les trous aimés et une humeur de sortie élevée. Il monte à 2, 4, 8 puis 16 points (choix 2).
  - Le niveau vit sur le visiteur du vivier, sans plafond (choix 8).
  - Une démission est définitive.
  - Green fee et cotisation selon le niveau, déjà en données dans C14.
  - Les évènements `MemberJoined`, `MemberPromoted` et `MemberResigned` alimentent les messages de C13.
- **Ligue** :
  - Les membres de la ligue sont les vrais membres du vivier, plus le pro.
  - La ligue ouvre une division de 8, pro compris, dès qu'il y a 7 membres. Une division s'ajoute par tranche de 8 (choix 3).
  - Tant qu'elle est fermée, un message le dit.
  - L'opinion fixe de C06 (`Member.Opinion`, `MinOpinion`) disparaît au profit de l'humeur cumulée.
  - « Le neuf » et les tests gardent un club pré-rempli, pour ne pas perdre la couverture de C06.
  - Une sauvegarde v13 convertit ses 40 membres générés en membres du vivier.
- **Pro et file** (choix 5) : un clic sur un groupe de la file l'envoie en tête de file (`World.JoinQueuedGroup(groupId)`), et le pro joue avec lui. Le groupe gagne l'événement « le pro joue avec nous ». Un membre accompagné gagne un point d'adhésion de plus. C'est un ordre de la sim, donc il est déterministe et sauvegardé, comme les autres commandes.
- **Jeu** :
  - une bulle de pensée de 3 s quand l'événement arrive, qu'une option coupe (choix 4) ;
  - l'info-bulle au survol (nom, niveau, humeur) ;
  - la fiche du golfeur (humeur, dernières pensées, niveau, points, parties jouées) ;
  - le rapport des réactions par trou dans un panneau ;
  - une carte des réactions sur le terrain.
  - Les textes sont en données (R4) et relus par Guillaume.

## Choix tranchés par Guillaume (2026-10-30)
1. En « Facile », les abandons sont rares : moins de 5 % sur « Le neuf » tel quel.
2. Adhésion par points, comme l'original : Member à 2, Silver à 4, Gold à 8, Platinum à 16. Les seuils sont en données.
3. La ligue ouvre dès 8 joueurs, pro compris, avec une division de plus par tranche de 8. Avant cela, elle est fermée.
4. Les pensées en bulle s'affichent au moment de l'événement et une option les coupe. L'historique est dans la fiche.
5. Un clic sur un groupe de la file : le groupe passe en tête et le pro joue avec lui, avec un bonus d'humeur et un point d'adhésion de plus pour les membres. Cela clôt le report de C15 (décision 20).
6. La météo n'a pas d'effet direct sur l'humeur : elle rend déjà les coups plus difficiles.
7. Quand un golfeur abandonne, ses partenaires perdent 1.
8. Pas de plafond de membres en C16.

Hypothèses de l'orchestrateur, réglables en données : la valeur de chaque événement part de celle de l'original quand il est cité (`docs/recensement-sim-golf.md` §9.4), sinon ±1 ; l'humeur de départ et la baisse par trou calent le critère 3.

Calage du lot 1 (résolveur physique, « Le neuf », 30 jours, graines 1, 2 et 3) : humeur de départ 6 (jamais au-dessus de 7, sinon le plafond de 10 avale les événements positifs : 35 % d'entre eux se perdaient à 10), baisse de 1 tous les 5 trous, contrariétés mineures à partir de la 6e occurrence, « coup décevant » à −1. Mesures :

| Graine | Golfeurs | Abandons | Humeur de sortie moyenne | Positifs écrêtés au plafond |
|---|---|---|---|---|
| 1 | 286 | 12 (4,2 %) | +6,03 | 24 sur 1 238 (1,9 %) |
| 2 | 295 | 8 (2,7 %) | +6,84 | 105 sur 1 567 (6,7 %) |
| 3 | 267 | 11 (4,1 %) | +4,29 | 0 sur 674 (0,0 %) |

Le résolveur simplifié ne rend ni eau, ni arbre, ni bunker, ni mauvais coup : il donne 0 abandon et une sortie à 9,9. Il ne peut donc pas caler. La CI cale sur le résolveur physique, 8 jours (48 golfeurs, graine 1) : au moins un abandon, moins de 8 %, sortie entre 1 et 9, moins de 10 % de positifs écrêtés. Les trois graines sur 30 jours se lancent à la main (`GOLFEROO_LONG=1`).

## Critères d'acceptation
1. L'humeur, les compteurs de réactions, les niveaux et points d'adhésion et l'état de la ligue entrent dans `Checksum` et `Save` (v14), et un cas `SaveTests` échoue si l'un d'eux est oublié. Une sauvegarde v13 se charge : ses 40 membres deviennent des membres du vivier, avec le même handicap et la même division.
2. Événements, chacun par un test qui vérifie l'évènement `MoodChanged`, pas un compteur :
   - un putt facile raté lève l'événement attendu ;
   - une balle dans l'eau aussi ;
   - une contrariété mineure isolée ne change pas l'humeur, sa répétition oui ;
   - un golfeur sous zéro lève `GolferQuit`, rentre au pavillon, et ses partenaires perdent 1.
3. « Le neuf », graine de référence, résolveur physique, 30 jours de printemps et d'été : moins de 5 % d'abandons (choix 1) et une humeur de sortie moyenne positive. Un trou modifié avec de l'eau devant le green fait baisser l'humeur moyenne de sortie sur ce trou, et ses compteurs « balle dans l'eau » montent. Les positifs écrêtés au plafond restent sous 10 %. Le critère se tient sur les graines 1, 2 et 3 (la mesure est dans les hypothèses ci-dessus). La CI tient une borne qui peut échouer sur 8 jours en résolveur physique (le résolveur simplifié est trop indulgent pour cela).
4. Adhésion : sur 60 jours de « Le neuf » à partir d'un club vide, au moins un visiteur devient Member, puis au moins un Member monte Silver. Les évènements `MemberJoined` et `MemberPromoted` sont levés. Un membre qui abandonne lève `MemberResigned` et ne revient jamais.
5. Ligue : à partir d'un club vide, la ligue reste fermée avec 6 membres et ouvre une division à 7. La journée de ligue suivante n'utilise que de vrais membres. Les tests de C06 et C21 passent sur le club pré-rempli.
6. Pro et file : `JoinQueuedGroup` met le groupe choisi en tête, et le pro part avec lui au prochain départ libre. Le groupe reçoit l'événement « le pro joue avec nous ». Avec la même graine et les mêmes commandes, on obtient le même `Checksum`.
7. Déterminisme : même graine, mêmes commandes, donc même `Checksum` après 10 jours avec humeur, abandons et adhésions.
8. Banc P1 : le tick tient les seuils de C12 (moyenne sous 2 ms, 99e centile sous 5 ms) avec les événements actifs.
9. Fumée Godot : une bulle de pensée apparaît au-dessus d'un golfeur après un événement, et aucune quand l'option est coupée. L'info-bulle et la fiche s'ouvrent. Le rapport par trou affiche les compteurs de la sim. Capture `c16-pensees` en CI.
10. Les textes des pensées sont en français, dans les données, un par événement.

## Lots
| Lot | Contenu | Contrôle |
|---|---|---|
| 1 | Humeur, événements, abandon, contagion, réactions par trou, green fee selon l'humeur, réputation sur l'humeur de sortie ; critères 2, 3, 7 et 8 | complet |
| 2 | Adhésion, démission, ligue branchée sur les vrais membres, migration v13, pro qui se joint à un groupe de la file ; critères 1, 4, 5 et 6 | complet |
| 3 | Pensées en bulle, info-bulle, fiche du golfeur, textes ; critères 9 (bulle, fiche) et 10 | léger |
| 4 | Rapport des réactions par trou, carte des réactions, capture ; critère 9 (rapport) | léger |

## Niveau de contrôle
Complet (lots 1 et 2), léger (lots 3 et 4).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
