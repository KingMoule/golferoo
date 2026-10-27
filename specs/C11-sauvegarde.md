# Sauvegarde de partie (F4, R7)
Statut : validé
Créé : 2026-10-27

## But
Fondation F4 de `docs/boucles-v1.md`, notée au cadrage de C10 comme chantier suivant : le joueur sauvegarde sa partie, quitte, et la retrouve telle qu'il l'a laissée. Aujourd'hui rien ne survit à la fermeture du jeu : `Main._Ready` recrée un `World` neuf à chaque lancement.

Ce qui prouve la réussite : une partie sauvegardée à n'importe quel tick, rechargée puis avancée de N ticks, a la même somme de contrôle (`World.Checksum`) qu'une partie jamais interrompue ; et dans le jeu, une sauvegarde rapide puis un chargement rendent le même parcours, les mêmes arbres, les mêmes golfeurs et la même ligue.

## Périmètre et hors périmètre
**Dans le périmètre**
- Tout l'état du monde entre deux ticks : terrain (relief, surfaces, départ, trou, par), gazon, arbres, golfeurs et groupes (balle en vol comprise), mode charge, calendrier, ciel imposé, état du RNG, files de commandes en attente, note du parcours, sources de beauté, ligue (membres, cartes, journées, divisions, champions, événements) et votre pro (compétences, entraînement).
- Un format binaire versionné et compressé, avec la somme de contrôle du monde dans l'en-tête.
- Dans le jeu : sauvegarde rapide, chargement rapide, sauvegarde automatique chaque jour de jeu, et `-- --load <fichier>` en ligne de commande.

**Hors périmètre, et où ça ira**
- Journal des commandes et rejeu (seconde moitié de F4) : ses usagers, les scénarios et leur test de rejeu en CI (D7) et le partage (D11), sont reportés depuis le 2026-10-24. Il viendra avec eux ; les files de commandes de `World` en sont déjà la base.
- Sauvegardes nommées et écran de chargement à plusieurs emplacements : avec la fondation d'UI (F7, preuve P3).
- Migration d'un ancien format : aucun joueur n'a encore de sauvegarde. Le numéro de version est écrit et vérifié dès la v1 ; la première migration s'écrira le jour où le format change après une version publique.
- Sauvegarde du tutoriel : C07 a tranché qu'il se refait depuis le début.
- Cloud, Steam, captures d'écran dans la sauvegarde.

## Conception
**Principe.** Chaque classe qui porte de l'état sait l'écrire et le relire : `internal void Write(SaveWriter w)` et un constructeur ou une fabrique `internal static X Read(SaveReader r, ...)`. Pas de réflexion ni de sérialiseur générique : c'est symétrique, lisible et rapide. Les caches (champs de flux, cartes de jeu, propriétés du sol, flaques, beauté, point de sortie) ne sont pas écrits : ils se recalculent au chargement, comme après une retouche.

**Format** (`src/Golferoo.Sim/Save/`)
- `SaveFormat` : en-tête `GOLF`, version (`1`), somme de contrôle du monde, puis le corps compressé (`GZipStream` de la bibliothèque standard ; aucune dépendance, la sim reste sans Godot).
- `SaveWriter` et `SaveReader` : enveloppes minces sur `BinaryWriter` et `BinaryReader` (doubles bit à bit, listes préfixées de leur longueur, `Point2`, `Vec3`).
- `World.Save(Stream)` et `World.Load(Stream, IShotResolver? resolver = null)`. `Load` vérifie le magique et la version, relit le monde, recalcule la somme de contrôle et lève `InvalidDataException` si elle diffère de l'en-tête. Le chargement passe par un constructeur privé de `World` qui ne génère rien (ni boisement, ni membres).

**Lot 1 : le monde sans la ligue** (`World*.cs`, `Terrain/`, `Turf/`, `Trees/`, `Golfers/`, `DeterministicRng.cs`)
- `HeightField` (coins), `TerrainGrid` (surfaces, départ, trou, par, `Blank`, nom), `TurfGrid` (hauteurs, usure, consignes), `TreeStand` (arbres et prochain identifiant ; l'index spatial se reconstruit par `Add`), `DeterministicRng` (ses quatre mots), golfeurs et groupes (y compris trajectoire de vol, coup prévu, départ différé), mode charge et points de passage, champs privés de `World` (décalage de jours, ciel imposé, jours de pluie, tirages de boisement, identifiant suivant) et les six files de commandes.
- `Save` lève `NotSupportedException` si le monde a une ligue : la ligue vient au lot 2.
- Tests (`tests/Golferoo.Sim.Tests/SaveTests.cs`) : critères 1 à 4.

**Lot 2 : ligue et pro** (`League/`, `Pro/`, `World.Pro.cs`)
- `LeagueSeason` (année, journées et leurs paires, finale, divisions, finalistes, classements, champions, événements, compteurs), `Member` (avis, points, cartes), `ScoreCard` si une carte est en cours, `ProSkills` (expérience, entraînement de l'année), état manuel du pro.
- Le refus du lot 1 disparaît ; les tests du lot 1 tournent aussi sur un monde avec ligue.

**Lot 3 : dans le jeu** (`game/src/Main.cs`, nouveau `game/src/SaveSlots.cs`, `project.godot`, `game/tests/Smoke.cs`)
- Actions `game_quicksave` (F5) et `game_quickload` (F8) dans l'`InputMap`. Fichiers `user://saves/rapide.golf` et `user://saves/auto.golf` ; F8 charge le plus récent des deux.
- Sauvegarde automatique au changement de jour de jeu (toutes les 6 min) : le monde est écrit en mémoire sur le fil principal, la compression et l'écriture du fichier se font sur une tâche de fond, pour ne pas faire sauter d'image.
- Chargement : `Main` garde le monde chargé dans un champ statique et recharge la scène ; `_Ready` part de ce monde au lieu d'en créer un neuf (même chemin de construction des couches qu'au lancement). Le `RatingJob` ne repart que si la note manque.
- Message bref à l'écran (« Partie sauvegardée », « Partie chargée », ou l'erreur) ; F5 refusé pendant le tutoriel, avec un message.
- `-- --load <fichier>` au lancement ; la fumée sauve, recharge et vérifie la somme de contrôle.

**Lots (3 PR)** : 1, puis 2, puis 3. Le banc (critère 6) vient avec le lot 2, quand le monde le plus lourd (ligue, 5 000 arbres) se sauve en entier.

## Choix tranchés
Guillaume a délégué le cadrage le 2026-10-27 (« je te fais confiance pour les recommandations ») : les choix ci-dessous sont pris par la session, avec leur raison.

1. **Format : binaire versionné et compressé**, plutôt que JSON. Le terrain et le gazon pèsent environ 0,4 Mo bruts par grille (`docs/boucles-v1.md`) ; en JSON les doubles perdent leur exactitude bit à bit ou demandent une écriture spéciale, et le fichier gonfle d'un facteur 3 à 5. Le binaire écrit exactement ce que la somme de contrôle vérifie. Perte : un fichier illisible à l'œil ; le test de rechargement compense.
2. **Sauvegarde à n'importe quel tick, balles en vol et files de commandes comprises**, plutôt que seulement « au calme » (entre deux journées). Un jeu de gestion se sauve quand le joueur le veut ; attendre un moment calme rendrait la sauvegarde rapide imprévisible. Les files sont de petits enregistrements, peu coûteux à écrire.
3. **Pas de journal des commandes dans C11** : voir hors périmètre. Gain : un chantier de 3 lots au lieu de 5, sans code sans usager.
4. **Un emplacement rapide et un automatique**, sans écran de sauvegardes nommées (qui attend la fondation d'UI). Touches F5 et F8 : les plus répandues dans les jeux PC ; F9 reste au ciel de debug.
5. **Somme de contrôle dans l'en-tête, vérifiée au chargement** : une sauvegarde corrompue ou un oubli dans un module se voient au chargement, pas des heures plus tard.
6. **Règle pour la suite** : tout nouvel état de la sim entre à la fois dans `Checksum` et dans `Save`, avec un cas dans `SaveTests`. Une ligne le rappellera dans la passation ; l'ajout à `CLAUDE.md` passera par une PR que Guillaume fusionne.

## Critères d'acceptation
1. Lot 1 : pour trois graines, sur le trou prototype et le dogleg, une partie sauvée au tick T (dont un T où une balle est en vol et un T où des commandes sont en file), rechargée et avancée de 2 000 ticks a la même `Checksum` qu'une partie jamais interrompue (`dotnet test --filter SaveTests`).
2. Lot 1 : sauver un monde rechargé donne exactement les mêmes octets que la sauvegarde d'origine.
3. Lot 1 : un fichier au magique faux, d'une version inconnue, tronqué, ou dont un octet du corps a changé, lève `InvalidDataException` ; aucun ne fait planter le test.
4. Lot 1 : un monde avec retouches de terrain, arbres plantés, rasés et tombés, gazon tondu et usé, et un changement de mois se recharge à somme de contrôle égale.
5. Lot 2 : les critères 1 et 2 tiennent sur un monde avec ligue, sauvé pendant une journée de ligue, après une finale, et après une partie d'entraînement du pro.
6. Lot 2 : le banc mesure la sauvegarde et le chargement du parcours chargé (ligue, 5 000 arbres) : sauvegarde en mémoire sous 50 ms, chargement sous 1 s, fichier sous 2 Mo.
7. Lot 3 : la fumée Godot sauve, recharge par la scène et écrit `SAVE OK` avec la même somme de contrôle avant et après, puis `SMOKE OK`.
8. Lot 3 : F5 puis F8 rendent le même état : même somme de contrôle (journal), captures `c11-avant` et `c11-apres` au même tick à comparer par Guillaume ; l'écriture de la sauvegarde automatique sur le fil principal (sérialisation en mémoire) prend moins de 5 ms sur le parcours de la scène, mesuré dans le journal.
9. Tous lots : `dotnet test Golferoo.sln`, `dotnet format Golferoo.sln --verify-no-changes` et les quatre jobs de CI verts.

## Niveau de contrôle
complet (sauvegardes, `docs/workflow.md`).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
