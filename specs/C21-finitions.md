# Finitions reportées (C21)
Statut : proposé
Créé : 2026-10-28

## But
Les défauts relevés depuis C08 et que Guillaume remarquera dès la première séance : des surfaces et une eau en escalier, un drapeau qui scintille, des coups trop dispersés, des golfeurs qui jouent toujours le choix parfait, une journée de ligue qui déborde sur la suivante et un banc du changement de jour qui frôle sa limite. Liste d'origine : `docs/handoff.md`, `docs/plan-alpha.md` (C21) et les constats ouverts de `specs/C12-parcours-plusieurs-trous.md`.

Ce qui prouve la réussite : les captures `c21-*` montrent des contours lisses ; les golfeurs touchent fairways et greens dans les proportions d'amateurs réels de leur niveau et se trompent parfois de façon plausible ; une journée de ligue de 18 trous tient entre deux journées ; la CI garde de la marge sur le banc.

## Décisions de Guillaume (cadrage du 2026-10-28, rondes 1 et 2)
1. **Dispersion plus regroupée** : un coup très loin du centre reste possible mais rare, et la dispersion se resserre avec le niveau. Calée sur des statistiques réelles d'amateurs, pour les golfeurs comme pour votre pro.
2. **Bordures lissées au rendu** (fairway, green, bunker et autres surfaces), avec une bande de transition ; la sim garde sa grille.
3. **Eau lissée** de la même façon, avec une rive en pente douce.
4. **La ligue joue tous les trous**, et **la journée passe de 6 à 9 min réelles à ×1** pour que la journée de ligue tienne avant la suivante.
5. **Profils de C09** : le fonceur prend plus de pénalités que le neutre ; l'artiste gagne environ 15 % de régularité dans un bel endroit.
6. **Décisions non optimales** : une part de hasard dans le choix du coup, d'autant plus grande que le golfeur est faible, et toujours plausible.
7. Rien d'autre à ajouter. Corrigés sans question : scintillement du drapeau, bannière du champion, légende de `c09-profils`, handicap faussé par un trou fermé, marge du banc du changement de jour. L'accueil des membres par le pro va en C16.

## Périmètre et hors périmètre
**Dans le périmètre** : les sept points ci-dessus, et l'attente des marcheurs pendant une retouche continue (constat ouvert de C12).

**Hors périmètre, et où ça ira** : l'équilibrage de l'économie après le passage à 9 min (plus de parties par mois) : C22, avec la partie longue. L'accueil des membres par le pro : C16.

## Conception
**Rendu** (`game/src/Rendering/TerrainRenderer.cs`, shader du terrain)
- Le shader lit la surface des cellules voisines et trace la frontière d'après un champ de distance interpolé (contours lisses, à la façon des *marching squares*), avec une bande de transition de quelques décimètres : rough tondu autour du fairway et du green, lèvre de sable du bunker. Aucune donnée nouvelle dans la sim ; si le shader ne suffit pas, une texture de masque par surface à la résolution double, construite au changement de surface.
- Eau : même contour ; rive en pente douce par le maillage de l'eau (ou du terrain sous l'eau), sans changer la hauteur de la sim.
- Drapeau : cause trouvée et corrigée (z-fighting, ombre, LOD ou mipmap ; à établir).

**Jeu** (sim)
- **Dispersion** : les écarts types des clubs (`Shots/Club.cs`) et la conversion handicap → niveau (`Golfers/SkillLevels.cs`) sont recalés pour atteindre les cibles du critère 4, sur la physique réelle (`PhysicalShotResolver`) ; la loi reste normale, sans troncature. Le pro suit les mêmes courbes par `SkillLevels.FromHandicap`.
- **Décisions non optimales** : dans `Shots/PlanJob.cs`, après le classement des candidats, le golfeur prend le meilleur, sauf avec une probabilité *p(h)* = 5 % + 15 % × h / 36 (h, son handicap) où il prend le deuxième ou le troisième, à condition que l'écart de score attendu reste sous 0,5 coup (sinon le meilleur). Tirage par le RNG du monde : déterministe.
- **Profils** : le poids des pénalités du fonceur et le gain de l'artiste (`Golfers/GolferProfile.cs`) réglés pour les critères 6 et 7.
- **Handicap** : l'écart compare le brut des trous joués à la note de ces mêmes trous.
- **Bannière et légende** : corrigées dans `LeaguePanel` et la capture `c09-profils`.

**Temps et ligue**
- `Calendar.TicksPerDay` passe de 6 à 9 min (`Time/Calendar.cs:13`). Tout ce qui se règle en jours (saisons, croissance, météo, entretien mensuel) garde sa durée en jours ; tout ce qui se règle en secondes de jeu (marche, swing) ne change pas. Les sommes de référence sont relevées à nouveau ; la sauvegarde change de version (pas de lecture des anciennes : aucune alpha distribuée).
- La ligue joue tous les trous du parcours ; si une journée ne finit pas avant la suivante malgré tout, la suivante attend la fin de la précédente (garde-fou testé, pas de chevauchement).

**Performance**
- Changement de jour : travail réparti sur plusieurs ticks ou rendu incrémental, pour une médiane sous 3,5 ms en CI.
- Retouche continue : un marcheur n'attend pas plus de 2 s de jeu son champ de flux pendant qu'on tient le pinceau.

## Lots
| Lot | Contenu | Contrôle |
|---|---|---|
| 1 | Rendu : bordures lissées, eau et rive, drapeau ; captures `c21-bordures` et `c21-eau` | léger |
| 2 | Jeu : dispersion recalée, décisions non optimales, fonceur et artiste, handicap, bannière et légende | complet |
| 3 | Temps : journée de 9 min, ligue sur tous les trous ; performance : changement de jour, attente des champs de flux | complet |

Les trois lots touchent des fichiers disjoints et peuvent avancer en parallèle ; le lot 3 fusionne en dernier (il relève les sommes de référence).

## Choix à trancher par Guillaume
Aucun. Un coup d'œil de sa part aux captures du lot 1 et, en jeu, au drapeau.

## Critères d'acceptation
1. Les captures `c21-bordures` et `c21-eau` (« Le neuf », vue rapprochée d'un green, d'un bunker et d'une rive) montrent des contours sans marches de cellule, avec la bande de transition.
2. Le lissage ne touche pas la sim : même graine, même somme de contrôle avant et après le lot 1 ; la mesure FPS de Guillaume ne baisse pas de plus de 3 %.
3. Le drapeau ne scintille plus (cause écrite dans la PR, vérifiée par Guillaume en jeu).
4. Test de la sim, 2 000 trous sur « Le neuf » par niveau : un handicap 18 touche 45 % ± 5 des fairways (par 4 et par 5) et 25 % ± 5 des greens en régulation ; un scratch 65 % ± 5 et 65 % ± 5 ; moins de 1 % des coups pleins finissent à plus de trois écarts types de la cible.
5. Test : un handicap 36 prend un choix non optimal sur 20 % ± 3 de ses coups, un scratch sur 5 % ± 2 ; aucun choix retenu n'est à plus de 0,5 coup attendu du meilleur ; même graine, même suite de choix.
6. Test : sur le dogleg de C09, le fonceur prend plus de pénalités que le neutre (aujourd'hui 9 contre 12).
7. Test : dans un bel endroit, la dispersion de l'artiste baisse d'environ 15 % (entre 12 et 18 %) par rapport au neutre.
8. Test : un trou fermé ne change plus l'écart de handicap d'un membre qui ne l'a pas joué.
9. Test : sur 18 trous et 12 groupes, une journée de ligue finit avant la suivante ; aucune journée n'en chevauche une autre.
10. Le banc tient : changement de jour, médiane sous 3,5 ms en CI ; tick moyen sous 2 ms et 99e centile sous 5 ms, inchangés.
11. Fumée : pinceau tenu 10 s, aucun marcheur n'attend son champ de flux plus de 2 s de jeu.
12. `SaveTests` passe ; les quatre jobs de CI sont verts ; `dotnet format` sans changement.

## Niveau de contrôle
léger pour le lot 1, complet pour les lots 2 et 3.

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
