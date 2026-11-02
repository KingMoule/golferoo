# Visée du pro à la souris
Statut : cadré (en attente du « go » de Guillaume)
Créé : 2026-11-02

## But
Retours de la première séance de jeu (2026-11-02, `c24-retours/retours-seance-1.md`, groupe C) : le mode visée du pro (C08) demande de régler un curseur de force à la main, les draws et fades sont timides, la balle part parfois loin de la visée, le clic sur le green tombe à côté, et le pro marche jusqu'à la balle même à l'entraînement. C27 rend la visée directe, plus arcade et plus lisible.

## Périmètre et hors périmètre
Dans le périmètre : visée au clic (force et dispersion qui suivent le clic), courbure accentuée du draw et du fade pour le pro, projection du clic et de la cible au sol, pro placé sur sa balle à l'entraînement, vérification de la balle « loin de la visée ».
Hors périmètre : swing contrôlable (B17), autres golfeurs (leurs coups restent ceux de C06 et C09), nouveaux clubs.

## Choix tranchés par Guillaume (2026-11-02)
1. **Hors de portée** : un clic plus loin que la portée du club joue la **force maximale** du club dans la direction du clic ; la cible affichée montre où la balle retombera vraiment.
2. **Dispersion** : **proportionnelle à la distance visée, avec un plancher** : un clic près de soi donne un coup précis, mais jamais parfait (plancher de 15 % de la dispersion du club).
3. **Draw et fade** : **style arcade, environ ×3** la courbure actuelle, réglable en données après quelques essais.

## Conception (à vérifier dans le code au lot 1)
- **Force par le clic** : `ProAim.cs` calcule déjà une force au clic (`√(d / portée)`) ; elle est rendue modifiable par le curseur. Le curseur disparaît : la force vient du clic seulement (molette ou touches pour un réglage fin, à décider avec les captures). Au-delà de la portée, force 1,0.
- **Dispersion** : la dispersion du coup du pro devient `club × max(plancher, distance visée / portée du club)` (valeurs en données), au lieu d'une valeur fixe par club. Le putt garde son modèle.
- **Courbure** : un coefficient de présentation `ProCurveScale` (3,0, en données) multiplie la courbure des draws et fades du **pro seulement**, pour garder les coups des autres golfeurs au bit près. Calage : un driver en draw plein courbe d'environ 45 m à 220 m, un fer 7 d'environ 24 m (valeurs de départ, à reprendre avec des captures de trajectoires).
- **Balle loin de la visée** : hypothèse : l'aperçu est volontairement faussé par les compétences du pro (« vent lu à ±x % », « pente lue à ±x° », C25), et une visée trop loin de la ligne vient de cette lecture et de la dispersion. À reproduire d'abord ; si c'est le comportement voulu, l'afficher mieux (l'ellipse de dispersion doit refléter l'erreur de lecture) ; sinon corriger.
- **Green** : le clic se projette sur le sol (rayon contre le terrain), la cible se pose au sol, pas à la hauteur de la balle. La pente que le pro lit à tort (C25) doit se voir, mais la cible reste là où l'on a cliqué.
- **Entraînement** : une partie d'entraînement place le pro sur la balle sans marcher ; les parties de ligue gardent la marche.

## Critères d'acceptation
1. Un clic au-delà de la portée joue la force maximale ; un clic à moitié de la portée joue une force qui fait atterrir la balle (sans vent ni dispersion) à ±2 % du point cliqué (test).
2. À club égal, la dispersion à mi-portée vaut environ la moitié de celle à pleine portée, sans descendre sous 15 % (test).
3. Un draw plein du driver courbe de 40 à 50 m à 220 m ; un fer 7 de 20 à 28 m ; les coups des autres golfeurs et les sommes de contrôle de la ligue sont inchangés (tests, banc).
4. Le curseur de force n'existe plus ; le mode visée marche à la souris seule ; fumée et capture `c27-visee`.
5. Sur un green, la cible est au sol sous le point cliqué (test de projection) ; capture `c27-green`.
6. En entraînement, le pro est sur la balle après chaque coup sans marcher (test).
7. Le cas « balle loin de la visée » est reproduit par un test ou expliqué par l'erreur de lecture, avec correctif ou affichage (rapport du lot 1).
8. `dotnet test Golferoo.sln` verte ; textes en français.

## Lots
1. **Modèle de coup** (sim) : dispersion proportionnelle avec plancher, `ProCurveScale`, force à la portée maximale, enquête « balle loin de la visée ». Critères 1 à 3 et 7. Contrôle **complet**.
2. **Interface de visée** : curseur retiré, clic projeté au sol, cible au sol, ellipse de dispersion, entraînement sans marche. Critères 4 à 6. Contrôle léger.

## Règle de livraison (briefs du codeur)
Avant « terminé » : `dotnet test Golferoo.sln -c Release`, `dotnet format --verify-no-changes`, fumée Godot (`SMOKE OK`) au lot 2, sorties montrées. Calage de la courbure par balayage, avec captures de trajectoires.

## Niveau de contrôle
complet (lot 1), léger (lot 2).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
