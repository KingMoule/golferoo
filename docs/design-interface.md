# Principes de l'interface

Validés par Guillaume le 2026-10-28 (C13), sur la maquette v2 : [Golferoo, thème d'interface](https://claude.ai/artifact/4VVy7snUANkKLMV3s8V4wz). Tout écran du jeu les suit ; on les modifie ici, pas écran par écran.

**En une phrase** : le cadre de Cities: Skylines (barre du bas, catégories, vignettes avec prix, verre dépoli), le caractère de SimGolf (couleurs de club, formes rondes et cernées, golfeurs qui ont une voix).

## Les dix principes
1. **Une barre à deux étages.** En haut, les outils en familles (construire, club, entretien, bulldozer) et les boutons ronds des panneaux ; en bas, l'état du club en pastilles : vitesse, heure et date, météo et saison, nom du club, membres, argent, humeur. À gauche, l'écusson du club et l'objectif en cours.
2. **Catégorie, puis vignette.** Un clic sur une catégorie ouvre un tiroir au-dessus de la barre ; un clic sur une vignette prend l'outil. Deux clics au plus pour tout.
3. **Le prix se voit avant le clic.** Chaque vignette porte son coût, l'info-bulle ajoute l'entretien, et le coût de l'action suit le curseur pendant qu'on peint ou qu'on pose.
4. **Verre et silhouettes.** Le cadre : verre sapin dépoli et translucide, filet clair d'un pixel, pastilles très arrondies, icônes en silhouettes pleines rangées par famille. Rien de plus dans le cadre.
5. **Le cartoon est dans ce qui vit.** L'écusson, les vignettes dessinées, les avatars, les visages d'humeur et les bulles des golfeurs sont cernés d'un trait sombre épais. Le cadre reste sobre, le contenu est joyeux.
6. **Une couleur, un sens.** Jaune drapeau pour la sélection, bleu ciel pour l'information, vert gazon pour le positif, brique pour l'alerte et la perte. Jamais une couleur décorative.
7. **Le terrain reste la vedette.** Les panneaux sont translucides ; le centre de l'écran n'est jamais couvert par défaut.
8. **Les golfeurs ont une voix.** Le fil **Birdie** (parodie de Twitter, comme Chirper) porte les messages du jeu à la voix des golfeurs et du club : courts, drôles, toujours une vraie info ; un clic suit le golfeur.
9. **Des panneaux qui s'empilent.** Chaque panneau monte de son bouton et s'ajoute à la pile sans fermer les autres ; il reste ouvert jusqu'à sa croix ronde en haut à droite ou Échap (le dernier ouvert d'abord).
10. **À la souris d'abord, et léger.** Tout se fait par un bouton (bulldozer compris) ; aucune touche n'est affichée tant que le plan de raccourcis n'existe pas. L'interface se redessine au changement, pas à chaque image : 144 FPS en 1080p.

## Palette
| Nom | Valeur | Usage |
|---|---|---|
| Sapin verre | `#182823` à 80 % | fond des panneaux et du tiroir |
| Sapin barre | `#243A32` à 74 % (étage haut), `#14221D` à 90 % (étage bas) | barre du bas |
| Pastille | noir à 38 % | champs de l'étage bas |
| Filet | `#F3ECD8` à 20 % | bord d'un pixel du verre |
| Encre | `#1D2A24` | traits épais du contenu cartoon |
| Crème | `#FFF8E6` (texte fort), `#E9E2CC` (icônes), `#C9C1AA` (texte secondaire) | texte et silhouettes |
| Drapeau | `#F4C542` | sélection |
| Ciel | `#9FD3F5` / `#7CC6F2` | information, vitesse, liens |
| Gazon | `#7BC86C` | positif, revenus, jauges |
| Brique | `#E8735A` / `#F08B72` | alerte, dépenses, bulldozer |

## Formes et typographie
- Verre : coins de 16 à 18 px, flou de l'arrière-plan (shader), ombre douce. Pastilles : arrondi complet, 34 px de haut. Boutons d'outil : 52 px, coins de 14 px ; boutons ronds : 44 px.
- Contenu cartoon : trait `#1D2A24` de 2 à 3 px, ombre pleine décalée vers le bas.
- **Fredoka** (titres, boutons, noms) et **Nunito** (texte, chiffres), libres (OFL), embarquées. À 1080p : 18 à 20 titre de panneau, 15 texte, 13 légende, jamais moins de 12.
- Icônes : silhouettes pleines, une couleur. Commandes génériques tirées du pack CC0 « Game Icons » de Kenney ; icônes du métier (golf, construction) dessinées en SVG dans le même style.
- Échelle d'interface 100 % en 1080p (référence), 125 % et 150 % proposés.
