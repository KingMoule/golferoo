# Brief commun : choix du moteur de Golferoo

Rédigé le 2026-10-24 par l'orchestrateur. Français, identifiants en anglais.

## La question de Guillaume
« Quel engin on prend pour bâtir ça ? Unity ou Godot ? Ou engin homemade ? Ou autre ? » Le nouveau jeu doit offrir **au minimum** les features de Sid Meier's SimGolf (2002), recensées dans `docs/recensement-sim-golf.md`. Ensuite viendront la modernisation de ces features, plus de profondeur, de nouvelles features et de nouvelles boucles de gameplay. Le choix doit anticiper cette suite, pas seulement le remake.

Attention au nom : malgré « simulateur de golf » dans le README, SimGolf est un **jeu de gestion de parcours** (tycoon isométrique, golfeurs simulés, construction, économie) avec un mode où l'on joue son pro.

## Matériaux
- `/home/claude/golferoo/docs/recensement-sim-golf.md` : inventaire des features (426 lignes). Lis les titres d'abord, puis les sections utiles.
- `/home/claude/crspytopgn/opensgolf/` : OpenSGolf, réimplémentation open source de SimGolf en **Rust**, licence **GPL-2.0-or-later**, v0.5.3, dernier commit 2026-10-22. Environ 45 k lignes dans `crates/sg-core` (décodeurs + règles du jeu, sans fenêtre ni audio, testé) et 59 k dans `crates/simgolf` (le jeu : miniquad/OpenGL, cpal, fontdue, rhai pour le scripting). Tourne sur Windows, macOS, Linux, Android et navigateur (`web/`). Un set graphique libre `packs/opensgolf-free`. Une ancienne version C++ dans `legacy/cpp`. Les `docs/DECODE_*.md`, `GAMELOGIC.md`, `MODDING.md`, `NEXT_STEPS.md`, `PARITY*.md` documentent les règles et chiffres extraits de l'exe. Lis par extraits (Grep/Glob, plages), jamais tout.
- `/mnt/project-files/sim_golf.zip` : le jeu original (fichiers propriétaires d'EA, non redistribuables). Ne l'extrais pas sauf besoin précis ; `unzip -l` suffit pour l'inventaire. Si tu l'extrais, fais-le dans ton propre dossier vide du scratchpad, n'exécute rien.
- `/home/claude/golferoo/CLAUDE.md` : workflow du dépôt. Contexte important : le développement est fait en grande partie par des agents Claude (orchestrateur Opus, codeurs Sonnet), sous la direction de Guillaume, qui fusionne les PR. La stack n'est pas encore choisie.
- Le web (WebSearch, WebFetch) pour l'état actuel des moteurs en octobre 2026 : versions, licences, frais, export web/mobile/console, maturité. Cite tes sources avec leur date ; distingue ce que tu as vérifié de ce que tu crois.

`/mnt/project-files` est un montage réseau : mets un `/` final à ses chemins, ne fouille jamais tout le dossier.

## Options à évaluer (toutes, puis d'autres si tu en vois de meilleures)
Unity ; Godot (GDScript, C#, ou GDExtension Rust/C++) ; moteur maison, y compris **partir d'OpenSGolf ou de son `sg-core`** (et ce que la GPL implique pour Golferoo : distribution, monétisation, consoles, fermeture du code) ; Bevy ou autre stack Rust ; une stack web (TypeScript + Pixi/Three/Babylon) ; Unreal ; toute autre piste sérieuse.

## Critères minimum
1. Couverture des features du recensement (simulation de centaines d'agents, terrain peignable et relief, pathfinding, UI dense de gestion, sauvegarde, sim-stories).
2. Anticipation de la suite : passage éventuel à la 3D ou à une vue libre, simulation plus profonde, modding, éventuel multijoueur ou partage de parcours, nouvelles boucles de gameplay.
3. Réutilisation réelle du code et du savoir existants (OpenSGolf, ses décodages, ses chiffres) et contraintes de licence.
4. Productivité d'un développement mené par des agents IA : code en texte lisible et diffable, tests headless et CI, scènes/assets en formats texte, qualité des connaissances des modèles sur le moteur, boucle de vérification sans éditeur graphique.
5. Plateformes et distribution (PC, Mac, Steam Deck, mobile, web, consoles), coûts et risques de licence du moteur, pérennité.
6. Performance, outillage art/animation, coût de production des assets.

## Ce que tu rends
Un fichier markdown à l'emplacement que te donne ton prompt, 250 à 450 lignes, structuré ainsi :
1. **Recommandation** en tête (une option, et un second choix), avec le niveau de confiance.
2. Ce que tu as vérifié (fichiers lus, commandes, sources web datées).
3. Analyse par option, avec un tableau de notation sur les critères.
4. Architecture proposée pour l'option recommandée : quelles couches, ce qui vient d'OpenSGolf (code, données, ou seulement savoir), comment la simulation reste testable headless.
5. Risques principaux et la vérification la moins chère qui trancherait chacun (ex. un prototype d'un jour).
6. Questions que seul Guillaume peut trancher (budget, plateformes cibles, open source ou commercial, 2D ou 3D).

Tu travailles seul : n'essaie pas de deviner ce que d'autres analystes diront. Ne modifie aucun fichier hors de ton fichier de sortie, ne commite rien.
