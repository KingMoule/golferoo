# Son minimal
Statut : cadré (en attente du « go » de Guillaume)
Créé : 2026-11-01

## But
Le jeu est muet (manque M9 de `docs/plan-alpha.md`, décision 5) : un jeu muet paraît cassé. C20 ajoute un son minimal, réglable : frappes, balle dans le trou, à l'eau, contre un arbre, ambiance météo, clics d'interface, quelques sons de gestion, deux musiques. Tout vit côté Godot ; la sim ne change pas (ni `Checksum`, ni sauvegarde).

## Périmètre et hors périmètre
Dans le périmètre : bus audio, curseurs d'options, banque de sons déclarée en données, lecteur de sons branché sur les évènements existants, musique menu et jeu, fichier de licences.

Hors périmètre : sons 3D positionnels et atténuation par la distance (tout en stéréo plat), voix, sons des bâtiments et du personnel, musique adaptative, nouveaux évènements dans la sim.

## Choix tranchés par Guillaume (2026-11-01)
1. **Origine** : mixte. Sons d'interface, caisse et eau **synthétisés** par un script du dépôt (aucune licence) ; frappes, balle dans le trou, arbre, ambiance et musique en **CC0 déposés par Guillaume** (mon conteneur n'atteint pas Kenney, OpenGameArt ni Freesound). Tant qu'un fichier manque, le son est simplement absent : le jeu ne plante pas.
   **Ajout (2026-11-02)** : Guillaume a déposé les packs Kenney (Impact, Interface, Casino, Digital Audio, CC0) et une boucle oiseaux-vent, et autorise, pour son usage personnel, les **sons** du pack `opensgolf-free` d'OpenSGolf (GPL-2.0-or-later ; dossiers `Golf_Sfx`, `Music`, ambiances, `Interface`, `Buy*`). Référence d'écoute dans les fichiers du projet, `c20-sons/`. Le lot 2 puise dans ces sources ; `LICENSES.md` note la licence de chaque fichier.
2. **Musique** : une piste pour le menu, une pour la partie, avec un curseur Musique séparé des Effets.
3. **Gestion** : oui, 4 sons : caisse (green fee), jingle d'objectif, ding du conseil du pro, alerte d'abandon.

## Conception
- **Bus** (`default_bus_layout.tres`) : `Master` → `Musique`, `Effets`, `Ambiance`. Trois curseurs dans les options (Musique, Effets, Ambiance) plus le général, 0 à 100 %, dans `GameOptions` / `options.cfg` (préférence de machine, hors sim). Valeur par défaut raisonnable (musique plus basse que les effets).
- **Banque** : `game/data/audio/sounds.json`, une entrée par évènement sonore (`id`, fichiers, bus, volume en dB, variation de hauteur, délai minimal entre deux lectures). Fichiers sous `game/assets/audio/`. Un fichier cité mais absent se journalise une fois et se saute ; une clé inconnue refuse le fichier (même règle que C14).
- **Lecteur** `Sfx` (autoload ou nœud de `Main`) : `Play(id)` avec petit pool de `AudioStreamPlayer`, variation aléatoire de hauteur, anti-rafale par id. Il utilise un RNG de présentation, jamais celui de la sim.
- **Branchements** (aucun nouvel évènement de sim, on lit ce qui existe) :
  - frappe selon le club (drive/bois, fer, wedge, putt) au départ de la balle ; balle dans le trou ; `ShotOutcome.Water` ; `ShotOutcome.Trees` ;
  - pour les golfeurs en vue seulement (au plus N frappes par seconde, pour ne pas faire un brouhaha à ×16) ; muet en pause et coupé à ×16 hors interface ;
  - clics d'interface : bouton, onglet, outil sélectionné, refus (achat impossible) ;
  - gestion : encaissement d'un green fee, `ObjectiveCompleted`, `AdviceGiven`, premier abandon d'un golfeur.
- **Ambiance** : boucle selon la météo et la saison du jour (oiseaux au beau temps, vent quand il souffle, pluie selon l'intensité), fondu enchaîné au changement ; rien la nuit ou en hiver sans oiseaux si le fichier manque.
- **Musique** : lecteur à deux pistes, fondu entre menu et partie, pause de musique sur le menu pause avec un fondu.
- **Licences** : `game/assets/audio/LICENSES.md` liste chaque fichier, sa source, sa licence et son auteur ; la fumée échoue si un fichier audio n'y figure pas.

## Critères d'acceptation
1. Les trois curseurs et le général existent dans les options, se sauvent dans `options.cfg`, se relisent, et un fichier absent ou corrompu retombe sur les valeurs par défaut (test comme `GameOptions` existant).
2. `sounds.json` : un oubli de champ ou une clé inconnue refuse le fichier ; un fichier audio absent est sauté sans exception (tests).
3. Chaque évènement du périmètre joue son id (test avec un lecteur factice qui enregistre les `Play`) ; un évènement hors vue ou en pause ne joue pas.
4. Anti-rafale : à ×16, le nombre de sons par seconde reste sous la borne (test).
5. Ambiance : la météo du jour choisit la bonne boucle (test sur les états de `Weather`).
6. Aucun changement du `Checksum` ni de la sauvegarde : `dotnet test Golferoo.sln` inchangé hors nouveaux tests.
7. Le script de synthèse régénère les sons synthétisés à l'identique (graine fixe) ; ils sont dans le dépôt.
8. Fumée Godot (`SMOKE OK`) : le bus layout se charge, chaque id de `sounds.json` se résout sans erreur, la fumée ne se fie pas à une carte son (pilote audio `Dummy` en CI).
9. Banc P1 inchangé (la sim n'est pas touchée) ; FPS : pas de baisse mesurable à l'écran de jeu.
10. Tous les textes sont en français ; `LICENSES.md` complet.

## Lots
1. **Socle et sons synthétisés** : bus, options, `sounds.json`, `Sfx`, script de synthèse, clics d'interface, caisse, eau, jingle d'objectif, ding du conseil, alerte d'abandon, `LICENSES.md`. Critères 1 à 4, 6 à 8, 10. Contrôle léger.
2. **Sons du golf, ambiance et musique** : frappes, trou, arbre, ambiance météo, musique menu et jeu, avec les CC0 de Guillaume. Critères 3 (reste), 5, 9. Contrôle léger. **Dépend de packs déposés par Guillaume** ; sans eux, le lot livre le câblage et les sons absents sont sautés.

## Règle de livraison (briefs du codeur)
Chaque brief reprend la règle de la PR #161 : **avant de dire « terminé », lancer la suite complète (`dotnet test Golferoo.sln`) et la fumée Godot (`SMOKE OK`) et montrer leur sortie.**

## Niveau de contrôle
léger (lots 1 et 2).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
