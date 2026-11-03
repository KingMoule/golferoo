# Retours de la séance 2
Statut : cadré (go de Guillaume le 2026-11-03)
Créé : 2026-11-03

## But
Deuxième série de commentaires de test (2026-11-03) : golfeurs laids au dézoom, groupes qui frappent chacun leur tour, chemins que les marcheurs devraient prendre, usure du gazon jugée laide, fleurs dans les bunkers, pro qui accueille les groupes.

## Périmètre et hors périmètre
Dans le périmètre : les 4 lots ci-dessous.
Hors périmètre : départ du groupe suivant dès que le précédent est sur le green (lot C de C28, ne pas refaire), textures d'arbres selon le zoom (C24 lot 4), assistant de trou (reporté).

## Choix tranchés par Guillaume (2026-11-03)
1. Golfeurs en modèle complet à tous les zooms.
2. Ordre de frappe comme au vrai golf : la balle la plus loin du trou joue la première ; le groupe marche ensemble une fois tous les coups joués.
3. Chemins : ceux que Guillaume trace avec l'outil terrain ; marche à ×4 au lieu de ×3 dessus, et la recherche d'itinéraire choisit le plus rapide (détour permis).
4. Mottes : un coup depuis un départ ou un fairway laisse un petit carré brun, jusqu'au prochain jour de pluie.
5. L'usure du gazon est supprimée : marche, sentiers, marques de green, réparation de nuit, surcouche H, humeur « gazon abîmé », conseil « gazon usé à 15 % » (C19).
6. Le pro marche au hasard autour du pavillon et salue les groupes (humeur +, une fois par groupe).

## Lots
1. **Rendu et accueil** (léger) : retirer le modèle simplifié des golfeurs ; fleurs hors des bunkers (vérifier d'abord si C26 A5 a déjà corrigé la cause) ; pro qui flâne autour du pavillon et salue (s'il n'est pas déjà fait par C28 lot A).
2. **Mottes à la place de l'usure** (complet) : supprimer l'usure et ses lecteurs (liste du choix 5), ajouter les mottes (état entré dans `World.Checksum` et `World.Save`, cas `SaveTests`, version de sauvegarde montée, fixture archivée) ; texture d'état : carré brun, repousse au jour de pluie ; captures.
3. **Chemins** (complet) : coût de marche ×4 sur une cellule de chemin, intégré au calcul d'itinéraire (champs de flux) ; test : sur un parcours avec chemin, le trajet par le chemin est choisi si plus rapide, et la vitesse est ×4 dessus.
4. **Frappe à tour de rôle** (complet, après la fusion du lot C de C28) : un tour de frappe par groupe, ordre par distance au trou décroissante, marche groupée après le dernier coup ; test d'ordre et de non-régression de la ligue.

## Critères d'acceptation
1. Aucun changement de modèle des golfeurs selon la distance de la caméra (test de scène ou capture).
2. Aucune fleur dans un bunker sur 100 graines (test).
3. Un coup depuis un départ ou un fairway crée une motte visible ; elle disparaît au premier jour de pluie ; plus aucune trace d'usure (test, capture `c29-mottes`).
4. Sur un chemin, la marche est ×4 ; l'itinéraire prend un chemin plus rapide même avec un détour (tests).
5. Dans un groupe de 4, les coups se jouent par distance décroissante puis le groupe marche d'un bloc (test).
6. Sauvegarde : chaque nouvel état échoue à `SaveTests` s'oublié ; 4 jobs de CI verts.
