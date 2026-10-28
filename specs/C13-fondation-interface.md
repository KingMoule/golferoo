# Fondation d'interface (F7, P3)
Statut : proposé
Créé : 2026-10-28

## But
Aujourd'hui tout passe par une trentaine de touches (B, F1 à F10, T, V, G, H, J, L, O, [, ]…) et un libellé de débogage en haut de l'écran ; chaque panneau (`TurfPanel`, `LeaguePanel`, `ProPanel`, `TutorialPanel`) a son propre style. Un joueur qui ne connaît pas le code ne trouve rien, et chaque écran à venir de l'alpha (finances, fiche du golfeur, bâtiments, menus) referait le sien (risque R3).

C13 pose **le thème et les composants communs**, puis **la barre du bas à la Cities: Skylines** d'où partent tous les outils et panneaux, et **Birdie**, le fil de notifications. Les principes visuels sont dans `docs/design-interface.md` (validés par Guillaume sur la maquette v2).

Ce qui prouve la réussite : une fumée Godot prend chaque outil et ouvre chaque panneau **par son bouton, sans touche** ; les captures `c13-*` ressemblent à la maquette v2 ; la mesure FPS de Guillaume reste au-dessus de **144 FPS en 1080p** avec l'interface affichée.

## Décisions de Guillaume (cadrage du 2026-10-28)
1. Thème hybride : la structure et la qualité des barres de Cities: Skylines 1 et 2, l'élément cartoon de SimGolf ; maquette v2 retenue.
2. Tout part de la **barre du bas**, comme dans Cities.
3. Icônes : pack CC0 de Kenney pour commencer ; de nouvelles plus tard, selon les principes.
4. Cible : 1080p, 144 FPS.
5. Infos de débogage (FPS, tick, golfeurs) cachées, F12 les affiche.
6. Notifications façon Chirper : le fil **Birdie**.
7. **Menus seulement** : pas de plan de raccourcis tant qu'on n'a pas toutes les touches. Les touches d'outils actuelles restent actives mais ne sont affichées nulle part ; celles de la caméra restent.
8. Panneaux : ils partent de la barre, **s'empilent sans fermer les précédents**, se ferment par Échap (le dernier ouvert d'abord) ou par la petite croix en haut à droite.
9. Bulldozer : efface ce qui a été posé sous le curseur (surface rendue au rough, arbres rasés ; plus tard, bâtiments).
10. Un bouton de la barre passe de la vue iso à la caméra libre (C10 : clic du milieu glissé pour tourner et incliner).

## Périmètre et hors périmètre
**Dans le périmètre**
- Thème Godot construit en code, polices Fredoka et Nunito, verre dépoli (shader), icônes Kenney, et sept composants : panneau, pastille, bouton d'outil, vignette, info-bulle, jauge, tableau.
- Barre du bas à deux étages, tiroirs d'outils à onglets et vignettes, bulldozer, boutons ronds des panneaux et de la caméra.
- Pile de panneaux ; migration des quatre panneaux existants sur les composants.
- Birdie à la place de `ShowMessage` et des avis de la ligue.
- Identifiants stables sur chaque élément ; le tutoriel les cible et ne cite plus de touches.
- Libellé de débogage derrière F12, échelle d'interface 100, 125 ou 150 %.

**Hors périmètre, et où ça ira**
- Prix réels, argent et écran des finances : C14 (la pastille d'argent et les prix des vignettes affichent « — » d'ici là ; le bouton Finances est grisé).
- Visages d'humeur, pensées des golfeurs dans Birdie, fiche du golfeur : C16 (emplacement prévu, caché).
- Objectif du club sous l'écusson, menu titre, écran d'options : C18 (l'écusson est là, l'objectif caché).
- Outils Bâtiments et Aménagements : C17 (boutons grisés, info-bulle « bientôt »).
- Sons d'interface : C20. Plan de raccourcis, nouvelles icônes, traduction : plus tard.

## Conception
**Code** : un dossier `game/src/Ui/`, sans règle de jeu (comme `Tools/`). La sim ne change pas ; aucun nouvel état de partie.
- `UiTheme` : palette et tailles de `docs/design-interface.md` en constantes, et le `Theme` Godot construit en code (styles de `PanelContainer`, `Button`, `Label`, `ProgressBar`, `TooltipPanel`…). Appliqué à une racine unique : un `CanvasLayer` `Ui` qui porte un `Control` plein écran `UiRoot` ; tous les éléments d'interface en descendent, pour que le thème s'hérite.
- `game/shaders/ui_glass.gdshader` : flou de l'écran derrière un panneau (lecture de `hint_screen_texture` à mip réduit), teinte et opacité de la palette. Un seul `BackBufferCopy` par image pour toute l'interface.
- Composants : `UiPanel` (titre, icône ronde, croix, contenu), `UiPill`, `UiToolButton` (silhouette ; états repos, survol, choisi, grisé), `UiThumb` (vignette, pastille de prix), `UiTooltip` (titre, description, lignes de coût), `UiGauge`, `UiTable`.
- `BottomBar` (deux étages) ; `ToolDrawer` (tiroir à onglets au-dessus de la barre, curseur de taille du pinceau) ; `PanelStack` (pile alignée de droite à gauche au-dessus de la barre ; Échap ferme le dernier ouvert) ; `Birdie`.
- `UiIds` : les identifiants stables en constantes (`Tool.Terrain`, `Thumb.Fairway`, `Panel.League`…), portés par le `Name` des nœuds (sans point : `ToolTerrain`) ; `UiRoot.Find(id)` les retrouve. Le tutoriel et la fumée s'en servent.
- Les outils (`Brush`, `TreeTool`, `PlaceTool`, `ProAim`) gardent leur logique ; ils gagnent des méthodes publiques que les boutons appellent (celles que leurs touches appellent déjà). `Main.HandleKey` reste, sans affichage des touches.
- Les clics sur l'interface ne touchent jamais le terrain : barre, tiroir et panneaux arrêtent la souris, le monde lit `_UnhandledInput`.
- Rafraîchissement au changement (jour, vitesse, météo, nouvel avis) ou à 4 Hz au plus pour l'heure ; jamais une chaîne reconstruite à chaque image.

**Barre du bas** (maquette v2)
- Étage haut : familles d'outils séparées par un filet. *Construire* : Terrain, Relief, Arbres, Trous. *Club* : Bâtiments, Aménagements (grisés). *Entretien* : Gazon, Vues. *Bulldozer*. À droite, boutons ronds : Ligue, Votre pro, Finances (grisé), Birdie, Caméra (iso ou libre).
- Étage bas, en pastilles : pause et vitesse (×1 à ×16), heure et date avec l'avancée du jour, météo, température, saison et prévision du lendemain, nom du parcours, membres (nombre de la ligue), argent (« — »), humeur (cachée).
- À gauche, l'écusson du club, dessiné en SVG dans l'esprit SimGolf, qui déborde au-dessus de la barre.
- Le libellé du trou suivi (« Trou 3, par 4 ») devient une pastille en haut au centre.

**Tiroirs et vignettes**
- Terrain : onglet Surfaces (Départ, Green, Fairway, Rough, Bunker, Eau, Bois, Chemin), vignettes dessinées dans les couleurs du rendu du terrain ; taille du pinceau.
- Relief : l'outil relief du pinceau et sa taille.
- Arbres : une vignette par essence pour planter un plant ou une motte, **rendue une fois au chargement à partir du modèle 3D** (`ThumbnailRenderer`, `SubViewport` hors écran, résultat gardé en mémoire ; servira aux bâtiments de C17) ; actions Sélectionner, Raser, Raser une zone, Garder les remarquables, Dégager le tronc. Faire tomber (debug) reste à la touche.
- Trous : Poser départ et trou, Nouveau trou, Supprimer, trou précédent et suivant.
- Gazon : ouvre le panneau des consignes. Vues : surcouche d'état du gazon (aujourd'hui H) ; d'autres vues plus tard.
- Bulldozer : pinceau qui rend la surface au rough et rase les arbres sous son rayon, avec les commandes existantes de `World`.
- Chaque vignette et chaque bouton a son info-bulle (nom, une phrase, coût et entretien en « — » jusqu'à C14).

**Panneaux et Birdie**
- `TurfPanel`, `LeaguePanel` (et sa carte de score), `ProPanel` passent dans `UiPanel` et dans la pile. `TutorialPanel` garde sa bulle du pro, restylée, hors de la pile.
- Le tutoriel ne cite plus de touches (« cliquez sur Terrain, puis sur Départ ») et met en évidence le bouton visé par son identifiant (anneau jaune qui pulse).
- Birdie : bouton rond (oiseau) en haut à gauche et fil de messages à côté : avatar, nom, `@pseudo`, temps, texte, mot-clic. Les nouveaux messages restent 8 secondes puis le fil se replie sur l'oiseau avec un compteur de non lus ; un clic rouvre l'historique (50 derniers) ; un bouton coupe les apparitions. Jusqu'à C16, les messages sont ceux du jeu (sauvegarde, avis de la ligue, saison, météo du jour), signés « Le club » ou « Votre pro ».

## Lots
| Lot | Contenu | Contrôle |
|---|---|---|
| 1 | `UiTheme`, polices, shader de verre, icônes, sept composants ; écran témoin `res://tools/UiGallery.tscn` et capture `c13-composants` | léger |
| 2 | Barre du bas, tiroirs, vignettes (dont `ThumbnailRenderer`), bulldozer, bouton caméra, info-bulles, débogage derrière F12, pastille du trou ; fumée « chaque outil par son bouton » ; capture `c13-interface` | léger |
| 3 | Pile de panneaux, Échap et croix, migration des quatre panneaux, tutoriel par identifiants et sans touches ; capture `c13-panneaux` | léger |
| 4 | Birdie, échelle d'interface (`-- --ui-scale 1.25`), captures `c13-birdie` et `c13-echelle-150` | léger |

Lots 1 puis 2 en série ; 3 et 4 en parallèle après 2 (zones disjointes : panneaux et tutoriel, Birdie et échelle).

## Choix à trancher par Guillaume
Aucun : les huit questions du cadrage sont tranchées. Une action de sa part : **déposer le pack « Game Icons » de Kenney** (zip CC0 de kenney.nl) dans le dossier du projet sous `c13/`, car le conteneur cloud ne peut pas télécharger depuis kenney.nl. En attendant, le lot 1 avance avec des silhouettes SVG provisoires (celles de la maquette), remplacées dès réception du pack.

## Critères d'acceptation
1. `UiTheme` applique la palette et les polices de `docs/design-interface.md` : la fumée vérifie que le style de panneau et la police d'un bouton de chaque calque d'interface viennent du thème.
2. `res://tools/UiGallery.tscn` montre les sept composants dans leurs états ; capture `c13-composants` dans le journal du job « Captures ».
3. La fumée prend, **par le signal `Pressed` de son bouton et sans aucun événement clavier**, chaque surface, le relief, chaque essence et chaque action d'arbre, chaque action de trou, le bulldozer, la vue d'état du gazon, chaque vitesse et la pause, et la caméra libre ; elle vérifie chaque fois l'état de l'outil (`Brush.Active` et sa surface, `TreeTool` et son mode, `PlaceTool.Active`, surcouche, `Speed`, `Paused`, `CameraRig.Mode`).
4. Un clic gauche simulé sur la barre ou sur un panneau, pinceau actif, ne peint aucune cellule (`TerrainGrid` inchangé).
5. Le bulldozer rend au rough les cellules sous son rayon et rase les arbres qui s'y trouvent (fumée : un carré de fairway et un arbre disparaissent).
6. Chaque panneau (Gazon, Ligue, Votre pro) s'ouvre par son bouton ; trois ouverts s'empilent sans se fermer ; Échap ferme le dernier, puis le précédent ; la croix ferme le sien.
7. Aucun texte affiché ne cite une touche (fumée : recherche des motifs « (T », « [b]F1 », « touche » dans les libellés de l'interface et du tutoriel).
8. Le tutoriel se termine en entier par les boutons seuls, et l'identifiant mis en évidence à chaque étape existe dans `UiRoot`.
9. F12 affiche et cache le libellé de débogage ; caché au lancement.
10. Birdie reçoit le message de sauvegarde (F5) et un avis de fin de saison de la ligue ; le compteur de non lus monte puis retombe à zéro à l'ouverture.
11. `-- --ui-scale 1.5` agrandit l'interface (capture `c13-echelle-150`) sans déborder de l'écran en 1080p.
12. Les captures `c13-interface`, `c13-panneaux` et `c13-birdie` montrent l'écran de « Le neuf » dans le style de la maquette v2.
13. Mesure de Guillaume (`Mesurer FPS.bat`, RTX 2080, 1080p) avec l'interface affichée : 144 FPS ou plus, et moins de 5 % sous la mesure sans interface.
14. Les quatre jobs de CI sont verts ; `dotnet format` sans changement.

## Niveau de contrôle
léger (écrans et outils de jeu, aucune règle de simulation touchée).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
