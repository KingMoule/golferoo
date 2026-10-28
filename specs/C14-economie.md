# Économie (B8 partiel)
Statut : validé
Créé : 2026-10-28

## But
Aucun choix n'a de prix aujourd'hui : seuls les arbres ont un coût (`TreeCosts`), cumulé dans `World.TreeSpending` sans portefeuille. C14 donne au club **un portefeuille et un grand livre** : la construction et l'entretien coûtent, les golfeurs paient un green fee à chaque trou et les membres une cotisation. C'est le préalable de la carrière (C18), de la faillite et de l'équilibrage (C22).

Ce qui prouve la réussite : en jeu, chaque coup de pinceau débite son prix, affiché avant le clic ; un test de la sim montre qu'un 9 trous fréquenté reste dans la bande de rentabilité fixée avec Guillaume.

## Décisions de Guillaume (cadrage du 2026-10-28)
1. Chiffres de jeu simples, à la SimGolf ; monnaie en dollars.
2. Green fee payé à la fin de chaque trou joué, selon l'humeur (neutre jusqu'à C16) et le niveau de membre.
3. Découvert permis avec une confirmation, 1 % d'intérêts par mois, sans limite sous 3 trous ouverts.
4. Pas de remboursement (repeindre, raser, bulldozer), sauf **annuler le dernier coup de pinceau dans les 5 secondes**.
5. Entretien mensuel par trou ouvert, sans salaires de personnel pour l'alpha.
6. Cible : un 9 trous fréquenté a un résultat d'exploitation positif dès la première année ; un parcours mal conçu perd de l'argent sans faire faillite avant la deuxième année.
7. **Membres : une cotisation et un green fee réduit.**
8. Le prix de l'action suit le curseur pendant qu'on peint ou qu'on pose, en plus de la vignette.
9. Tous les paramètres sont proposés ci-dessous (« Paramètres de Facile »), tranchés avec Guillaume ; ils vivent en données, pas en code.

## Périmètre et hors périmètre
**Dans le périmètre**
- Portefeuille et grand livre par catégorie et par mois dans la sim ; fichier de paramètres de difficulté `data/economy/facile.json`.
- Coûts de construction : peinture par surface et par cellule réellement changée, relief par m³ déplacé, nouveau trou, arbres (les constantes de `TreeCosts` passent en données ; `TreeSpending` disparaît dans le grand livre).
- Entretien mensuel par trou ouvert, majoré par des consignes de gazon exigeantes ; intérêts du découvert ; limite de découvert au-delà de 3 trous ouverts.
- Revenus : green fee par trou joué, cotisations mensuelles des membres (les membres de la ligue actuelle, tous au niveau Member jusqu'à C16).
- Annulation du dernier coup de pinceau (peinture ou relief) avec remboursement.
- Écran des finances, pastille d'argent et prix réels dans la barre (C13), prix sous le curseur, confirmation du passage au découvert.

**Hors périmètre, et où ça ira**
- Niveaux Silver, Gold, Platinum et humeur réelle : C16 (les paramètres existent déjà, la sim les lit quand ils arrivent).
- Bâtiments, ventes du snack-bar et du pro shop : C17. Faillite, argent de départ par difficulté au menu, bilan annuel : C18. Partie longue de deux ans : C22.
- Prêts, budgets détaillés, prix réglables par le joueur (B7 refusé le 2026-10-24) : bêta.

## Conception
**Sim** (`src/Golferoo.Sim/Economy/`, nouveau)
- `EconomyParams` : lu depuis `data/economy/<difficulté>.json` (même façon de lire que `CourseLoader`), valeurs en cents pour rester entières et déterministes. « Le neuf », les tests et le tutoriel utilisent Facile.
- `Ledger` : solde (cents, `long`), totaux par catégorie (Surfaces, Relief, Trous, Arbres, Entretien, Intérêts, Green fees, Cotisations) pour le mois courant et les 24 derniers mois. Entre dans `World.Checksum` et `World.Save` (cas `SaveTests`). Évènements `MoneyChanged` et `MonthClosed` pour le jeu.
- Débit au moment où la sim applique la commande (début de tick), d'après ce qui a réellement changé : cellules dont la surface change, volume de terre ajouté ou retiré, arbres rasés. Une commande qui ferait dépasser la limite de découvert est refusée en entier (évènement `PurchaseRefused`), jamais appliquée à moitié.
- Clôture de mois (au changement de mois de `Calendar`) : entretien, intérêts sur le solde négatif, cotisations, puis ligne du mois dans le grand livre.
- Green fee : à l'enregistrement d'un trou sur la carte du golfeur (`ScoreCard.Record`), tarif du niveau (visiteur si `MemberId` est -1) × facteur d'humeur. Un trou non joué (fermé ou abandon) ne paie rien.
- Annulation : la sim garde l'état d'avant du **dernier** coup de pinceau (cellules et hauteurs touchées) ; le jeu délimite le coup (bouton pressé puis relâché) et n'offre l'annulation que 5 secondes réelles. Toute autre commande de terrain efface cette mémoire. Les arbres rasés par la peinture ne reviennent pas (dit dans l'info-bulle). L'état d'annulation entre dans `Checksum` et `Save`.

**Jeu** (lot 3, après C13 lot 2)
- La pastille d'argent de la barre affiche le solde (vert positif, brique négatif) et le résultat du mois ; le bouton Finances ouvre un `UiPanel` : solde, revenus et dépenses du mois par catégorie, douze derniers mois en barres.
- Les vignettes et info-bulles de C13 affichent les prix et l'entretien réels ; pendant un coup de pinceau, une pastille près du curseur affiche le coût cumulé du coup.
- Première commande qui ferait passer le solde sous zéro : fenêtre de confirmation (une fois par passage au découvert). Commande refusée par la limite : message dans Birdie si C13 lot 4 est livré, sinon dans la pastille d'information.
- Bouton « Annuler » visible 5 secondes après un coup de pinceau, dans le tiroir de l'outil.

## Lots
| Lot | Contenu | Contrôle |
|---|---|---|
| 1 | `EconomyParams` et `facile.json`, `Ledger`, coûts de construction (surfaces, relief, trous, arbres), entretien, intérêts, limite de découvert, `Save` et `Checksum`, migration de `TreeSpending` | complet |
| 2 | Green fees et cotisations, clôture de mois, annulation du dernier coup, test de rentabilité | complet |
| 3 | Écran des finances, pastille d'argent, prix réels dans la barre et sous le curseur, confirmation de découvert, bouton Annuler ; capture `c14-finances` | léger |

Lots 1 et 2 en série, en parallèle de C13 (zone `src/Golferoo.Sim/` disjointe) ; lot 3 après C13 lot 2.

## Paramètres de Facile

Proposition du 2026-10-28, ronde 2 tranchée par Guillaume (cotisation et green fee réduit ; prix sous le curseur). Tous ces chiffres iront dans un fichier de données (`data/economy/facile.json`) : on les règle sans toucher au code, et la partie longue headless (C22) servira à les équilibrer.

Repères du jeu : un mois = 10 jours de jeu, une année = 120 jours (12 h réelles à ×1, 45 min à ×16). « Le neuf » couvre environ 1 200 × 800 m. Un par 4 typique : fairway d'environ 12 000 m², green 500 m², départ 300 m², deux bunkers de 150 m².

### Argent
| Paramètre | Proposé | Pourquoi |
|---|---|---|
| Argent de départ | 150 000 $ | 6 à 7 trous construits avant de toucher au découvert |
| Intérêts du découvert | 1 % par mois | se sent, sans tuer une partie |
| Découvert | permis avec confirmation, sans limite sous 3 trous ouverts, puis limité à −100 000 $ | comme l'original, avec un garde-fou ensuite |
| Seuil de faillite (C18) | trésorerie sous −100 000 $ six mois de suite | décision du plan |

### Construction (une fois, à la peinture ou à la pose)
| Action | Proposé | Un par 4 typique |
|---|---|---|
| Fairway | 1 $ / m² | 12 000 $ |
| Green | 6 $ / m² | 3 000 $ |
| Départ | 4 $ / m² | 1 200 $ |
| Bunker | 3 $ / m² | 900 $ |
| Eau (creuser) | 4 $ / m² | selon le dessin |
| Chemin | 2 $ / m² | selon le dessin |
| Rough (repeindre) | 0,2 $ / m² | quasi gratuit, c'est le fond |
| Sous-bois (repeindre en Bois) | 1 $ / m² | |
| Relief | 2 $ par m³ de terre déplacée | une colline modeste : quelques milliers de dollars |
| Nouveau trou (drapeau, repères) | 2 000 $ | |
| **Total d'un par 4 typique** | | **environ 20 000 $** |
| Bulldozer | gratuit, aucun remboursement | décision « pas de remboursement » |

Arbres : les coûts actuels de C02 changent peu, sauf le défrichage, aujourd'hui à 5 $ / m² (défricher un couloir de fairway coûterait 60 000 $, trois fois le trou).
| Action | Actuel | Proposé |
|---|---|---|
| Planter un plant | 50 $ | 50 $ |
| Planter une motte | 800 $ | 800 $ |
| Raser un arbre (remarquable ×2) | 200 $ | 150 $ |
| Défricher le sous-bois | 5 $ / m² | 0,5 $ / m² |
| Dégager un tronc tombé | 150 $ | 100 $ |

### Entretien (chaque fin de mois)
| Poste | Proposé | Pour un 9 trous |
|---|---|---|
| Par trou ouvert (tonte comprise : les tondeuses ne sont que visuelles dans la sim) | 1 100 $ / mois | 9 900 $ |
| Consignes de gazon exigeantes (greens rapides, rough court) | +20 % sur l'entretien des trous | |
| **Total d'un 9 trous** | | **environ 10 000 $ / mois, 120 000 $ / an** |

### Revenus
| Paramètre | Proposé |
|---|---|
| Green fee d'un visiteur, par trou joué | 15 $ (135 $ la partie de 9 trous) |
| Membre, Silver, Gold, Platinum : green fee | 12, 10, 8 et 6 $ par trou |
| Membre, Silver, Gold, Platinum : cotisation | 360, 600, 960 et 1 440 $ par an, prélevés par douzièmes à chaque fin de mois (décision de Guillaume : cotisation et green fee réduit) |
| Effet de l'humeur à la fin du trou | de ×0,5 (humeur −10) à ×1,2 (humeur +10), ×1 à humeur neutre en attendant C16 |
| Un golfeur qui abandonne | ne paie pas les trous restants |

### Ce que ça donne, en ordre de grandeur
Un 9 trous fréquenté : 12 paires par jour l'été, 6 au printemps et à l'automne, presque rien l'hiver (bornes réelles fixées à C15). Cela fait environ 720 paires par an, soit 13 000 trous joués par des golfeurs, donc autour de 195 000 $ de green fees pour 120 000 $ d'entretien : environ +75 000 $ par an. Partie en facile : 150 000 $ au départ, environ 180 000 $ pour construire 9 trous, un court passage au découvert la première saison, puis une trésorerie positive avant la fin de la première année.

### Cible d'équilibrage (pour le test de rentabilité de C14 et la partie longue de C22)
- Un 9 trous bien conçu et fréquenté a un **résultat d'exploitation positif dès la première année** (green fees moins entretien), et sa **trésorerie redevient positive avant la fin de la première année**.
- Un parcours mal conçu (golfeurs mécontents, peu de visiteurs) perd de l'argent chaque mois, mais n'atteint pas le seuil de faillite avant la deuxième année.

## Critères d'acceptation
1. `facile.json` contient tous les paramètres ci-dessus ; un test vérifie qu'un paramètre manquant ou négatif est refusé au chargement, et que changer une valeur du fichier change le coût débité (aucun prix en dur dans le code).
2. Peindre 100 m² de fairway sur du rough débite exactement 100 $ ; repeindre du fairway en fairway ne coûte rien ; relever le relief débite selon le volume réel déplacé ; raser un arbre remarquable coûte le double.
3. Au-delà de 3 trous ouverts, une commande qui ferait passer le solde sous −100 000 $ est refusée entière, rien n'est appliqué, `PurchaseRefused` est levé ; sous 3 trous, elle passe.
4. À chaque fin de mois : entretien de 1 100 $ par trou ouvert (+20 % avec des consignes exigeantes), 1 % d'intérêts sur un solde négatif, cotisations des membres au douzième ; une ligne de mois s'ajoute au grand livre.
5. Un visiteur qui joue un trou paie 15 $, un membre 12 $ (humeur neutre) ; un trou fermé ou abandonné ne paie rien.
6. Annuler juste après un coup de pinceau rend les cellules, les hauteurs et l'argent exacts ; après une autre commande de terrain, l'annulation n'est plus possible.
7. Même graine, mêmes commandes : même solde et même somme de contrôle ; sauver et recharger au milieu d'un mois redonne le même solde, le même grand livre et la même annulation possible (`SaveTests`, qui échoue si un champ est oublié).
8. Test de rentabilité (« Le neuf », `StubShotResolver`, arrivées scriptées de 12, 6 et 1 paires par jour selon la saison) : une journée de jeu par niveau d'affluence est simulée, et la projection annuelle (revenus − entretien) est positive ; avec moitié moins de visiteurs et des consignes exigeantes, la perte annuelle reste au-dessus du seuil de faillite de C18 (−100 000 $ six mois de suite). La partie réelle de deux ans viendra avec C22.
9. Le banc reste dans ses seuils (tick moyen sous 2 ms, 99e centile sous 5 ms) avec l'économie active.
10. Jeu : la pastille d'argent et l'écran des finances suivent le grand livre ; le prix sous le curseur égale le débit du coup ; capture `c14-finances`.
11. Les quatre jobs de CI sont verts ; `dotnet format` sans changement.

## Niveau de contrôle
complet (lots 1 et 2 : état de partie et argent), léger (lot 3 : écran).

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
