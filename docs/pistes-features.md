# Pistes de features pour Golferoo : première liste à trier

Mis à jour : 2026-10-24. Chantier R03. Liste de départ pour la séance de brainstorming avec Guillaume. Elle part de trois sources :
- les faiblesses de l'original relevées par les tests de l'époque et par OpenSGolf (`docs/recensement-sim-golf.md`, sections 18 et 21) ;
- le contenu coupé de l'original (section 20) ;
- ce que la grille fine, la 3D et la simulation déterministe rendent possible (`docs/faisabilite-features.md`).

Rien n'est décidé. Chaque piste a un effort (S, M, L, XL, comme dans R02) et les risques de R02 qu'elle touche. La colonne **Tri** est à remplir pendant la séance : *oui* (première version), *plus tard*, *non*.

## Ce qu'on garde en tête en triant
- La devise de l'original : un trou doit « avoir l'air dur et se jouer facilement ». Une piste qui ajoute de la gestion sans rien changer à la façon de construire ou de regarder son parcours s'éloigne du jeu.
- Le cœur de l'original est une boucle courte : **construire un trou, regarder les golfeurs le jouer, lire leurs réactions, corriger.** Les meilleures pistes la nourrissent.
- R1 (parcours plein) passe avant tout le reste : une piste qui ajoute beaucoup d'agents ou de calculs par golfeur coûte plus cher qu'il n'y paraît.

## A. Corriger les faiblesses de l'original

| # | Piste | Pourquoi | Effort | Risques | Tri |
|---|---|---|---|---|---|
| A1 | Vitesse du temps réglable (×1 à ×16, déjà dans la sim) et avance rapide jusqu'au prochain événement | un tournoi durait plus de deux années de jeu, sans accélération | S | R1 | |
| A2 | Caméra : sauter de trou en trou, suivre un golfeur, revenir au pavillon | défilement lent et pas de raccourci, reproché par les tests | S | | |
| A3 | Messages regroupés et triés par importance, journal consultable | messages qui se chevauchent et disparaissent | S | R3 | |
| A4 | Pensées et dialogues générés par morceaux pour varier les formulations | dialogues répétitifs | M | R4 | |
| A5 | Golfeurs plus grands à l'écran et plus expressifs (animations de réaction) | golfeurs petits et sommaires | M | R2 | |
| A6 | Tutoriel et conseiller jouables, pas seulement sur Easy la première année | le jeu livré n'a pas de tutoriel | M | R3, R4 | |
| A7 | Plus de 18 trous et de 50 × 50 tuiles : 27 ou 36 trous, carte plus grande | limites de l'original, parcours vite encombrés | M | R1 | |
| A8 | Accomplissements Grand Slam atteignables, record du parcours annoncé | bugs de l'original | S | | |

## B. Approfondir les features existantes

### Construction
| # | Piste | Effort | Risques | Tri |
|---|---|---|---|---|
| B1 | Plusieurs départs par trou (rouge, blanc, bleu, noir) et positions de drapeau à changer au fil des jours | M | | |
| B2 | Outils de forme : fairway et bunker tracés à la courbe, lissage du relief, copier-coller d'une zone | M | | |
| B3 | Gabarits de trous (par 3 en île, dogleg, par 5 à risque) à poser puis retoucher | S | | |
| B4 | Analyse de coup étendue : carte de chaleur des chutes de balle, trajectoires par profil de golfeur | S | R1 | |
| B5 | Rejouer les coups marquants (la sim est déterministe : rejouer une graine suffit) | M | | |
| B6 | Cascades, murets, ponts et rives travaillés (voir R02, section 4) | M | | |

### Économie
| # | Piste | Effort | Risques | Tri |
|---|---|---|---|---|
| B7 | Fixer soi-même le green fee, les cotisations et les prix des services (l'original ne le permet pas) | S | | |
| B8 | Prêts bancaires, entretien des bâtiments, budget d'entretien du terrain (l'original n'en a pas) | S | | |
| B9 | Rapports financiers en vrais graphiques, prévisions simples | M | R3 | |

### Golfeurs
| # | Piste | Effort | Risques | Tri |
|---|---|---|---|---|
| B10 | Handicap qui progresse, mémoire des trous joués (« j'ai raté ce bunker la dernière fois ») | M | | |
| B11 | Profils plus variés : juniors, seniors, familles, groupes de quatre, golfeurs d'affaires pressés | M | R1 | |
| B12 | Relations persistantes entre membres (amis, rivaux) qui alimentent les histoires | M | R4 | |
| B13 | Golfeurs qui prennent des risques selon leur caractère (prudent ou téméraire sur le même trou) | S | | |

### Histoires, VIP et célébrités
| # | Piste | Effort | Risques | Tri |
|---|---|---|---|---|
| B14 | Histoires construites à partir de morceaux réutilisables plutôt qu'une liste figée | L | R4 | |
| B15 | Plus de VIP aux effets variés (journaliste, investisseur, maire, écologiste) | M | R4 | |
| B16 | Célébrités qui jouent des parties et attirent des spectateurs | M | R1, R2 | |

### Votre pro et les tournois
| # | Piste | Effort | Risques | Tri |
|---|---|---|---|---|
| B17 | Contrôle du swing optionnel (jauge de puissance et d'effet), en plus de la visée de l'original | M | | |
| B18 | Tournois sur plusieurs tours avec cut, calendrier d'une saison de tournois | M | R1 | |
| B19 | Spectateurs, tribunes, caméras de télévision pendant les tournois | L | R1, R2 | |
| B20 | Autres formats : skins, scramble, match play en équipe | S | | |

## C. Contenu coupé à faire revivre

| # | Piste | Effort | Risques | Tri |
|---|---|---|---|---|
| C1 | Landmarks coupés : chapelle, jardins suspendus, voie ferrée, rocaille, ruines de cathédrale, pyramide, volcan | M | R5 | |
| C2 | Décors coupés : bambou, pin noir, étang à carpes koï, source chaude, oasis, fontaine du désert | M | R5 | |
| C3 | Accessoires des maisons de célébrités (piscine, jacuzzi, voitures), qui augmentent la valeur des lots voisins | M | R5 | |
| C4 | Chariot à boissons et voiturette du ranger : employés mobiles plutôt qu'à poste fixe | S | R2 | |
| C5 | Visiteur célébrité qui achète une maison (code mort de l'original) | S | R2 | |
| C6 | Écran de choix de la paire au départ (code sans appel dans l'original) | S | | |

## D. Nouvelles boucles de jeu

Chacune est une boucle à part entière, qui ajouterait une raison de revenir au jeu. Elles sont classées de la plus proche à la plus éloignée du cœur de l'original.

| # | Boucle | Ce que fait le joueur | Ce qu'elle apporte | Effort | Risques | Tri |
|---|---|---|---|---|---|---|
| D1 | **Météo et saisons** | composer avec le vent, la pluie et les saisons ; fermer ou ouvrir le parcours | le vent agit sur la balle (la physique existe) ; l'hiver donne un rythme à l'année | M | R1 | |
| D2 | **Agronomie et entretien** | arroser, tondre, régler la vitesse des greens, réparer l'usure des zones jouées | une gestion quotidienne qui modifie le jeu lui-même : greens rapides, fairways fermes ou gras | M | | |
| D3 | **Réputation et presse** | lire les critiques, viser les classements des magazines, répondre aux avis | un objectif lisible au-delà de l'argent ; prolonge l'évaluation SGA | M | R4 | |
| D4 | **Événements privés** | louer le club pour des tournois d'entreprise, des œuvres caritatives, des mariages | revenus ponctuels avec des exigences propres (un mariage veut un beau paysage, une entreprise un jeu rapide) | M | R4 | |
| D5 | **Ligues de membres** | organiser une ligue au fil de la saison, suivre le classement | donne un sens à l'adhésion et des rivalités entre membres (B12) | S | | |
| D6 | **École de golf** | former des juniors qui progressent et deviennent pros | une boucle longue et attachante, liée au pro jouable | M | R2 | |
| D7 | **Scénarios** | relever un défi écrit : sauver un club en faillite, construire un links en 3 ans, un parcours sur un terrain impossible | des parties courtes et rejouables, un bon terrain pour le tutoriel | M | R4 | |
| D8 | **Écologie** | préserver la faune, gérer l'eau, obtenir une certification environnementale | prolonge la faune de l'original ; tension entre beauté, coût et jeu | M | | |
| D9 | **Rivaux** | un club concurrent sur la carte du monde vous dispute les golfeurs et les tournois | une pression extérieure au parcours | L | | |
| D10 | **Chaîne de clubs** | garder ses anciennes propriétés et en gérer plusieurs (l'original oblige à quitter l'ancienne) | une progression de fin de partie | L | R1 | |
| D11 | **Partage et défis** | publier un parcours (Steam Workshop), battre le score de son pro sur le parcours d'un autre | durée de vie et communauté ; la sim déterministe permet des scores vérifiables | L | R6 | |
| D12 | **Mode photo et visite libre** | se promener sur son parcours au ras du sol, prendre des photos | montre la 3D, que l'original n'avait pas ; prolonge SimFoto | S | | |
| D13 | **Golf de nuit** | éclairer des trous pour jouer le soir (balles lumineuses, practice éclairé) | un rendu nouveau et des revenus en soirée | M | | |

## Après le tri
Les pistes gardées passeront dans `docs/chantiers.md` sous forme de chantiers à cadrer, dans l'ordre fixé avec Guillaume, après les preuves P1 à P3 de R02. Les pistes *plus tard* restent ici.
