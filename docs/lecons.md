# Leçons en attente

Journal des frictions du workflow, une ligne chacune, en attendant la prochaine revue `/ameliorer`.
Une friction : une règle ignorée, un aller-retour évitable, un agent qui a relu ce qu'il aurait pu recevoir, une sortie qui a noyé le contexte, un audit qui a raté quelque chose, une consigne qui a coûté plus qu'elle n'a rapporté.

Format : `- AAAA-MM-JJ · famille · ce qui s'est passé · coût estimé · source (chantier, PR)`
Familles : `contexte`, `délégation`, `vérification`, `règle-ignorée`, `outil`, `spec`, `autre`.

Ajouter sans relire tout le fichier : `echo "- ..." >> docs/lecons.md`. `/ameliorer` vide la liste une fois les leçons traitées.

## À traiter
- 2026-10-24 · outil · conteneur cloud sans .NET ni Godot ; dotnet-install.sh refusé par le proxy, `apt-get install dotnet-sdk-10.0` marche, Godot seulement en CI · quelques minutes · C01 lot 1
- 2026-10-24 · délégation · les agents du dépôt (codeur, auditeur, relecteur) ne sont pas chargés dans la session cloud de projet : repli sur general-purpose en Sonnet avec renvoi au fichier .claude/agents/<rôle>.md · un appel raté · C01 lot 2
- 2026-10-24 · outil · branche recréée depuis main + cherry-pick non poussé : le codeur a trouvé l'ancien commit en distant et a dû fusionner · un aller-retour · C01 lot 4
- Retouche par brief d'une ligne (C01 lot 8) : l'agent a arrondi l'arbre de repli par code alors que le jeu affiche tree.glb (script Blender) ; une source d'asset doit être évidente depuis le code qui l'affiche, ou le brief la nomme.
- 2026-10-25 · brief · le brief citait FlowField.BuildCosts, interne sans InternalsVisibleTo : le codeur a testé via le cache de champs · aucun aller-retour · C02 lot 1
- 2026-10-25 · CI · le nombre de captures attendu vivait à deux endroits (Captures.cs et ci.yml) et a cassé la CI deux fois : ci.yml ne vérifie plus que CAPTURES OK, Captures.cs fait foi · deux allers-retours CI · C02 lots 1 et 3
- 2026-10-25 · brief · un critère « identique au bit près » demande une référence calculée avant le changement : le brief doit dire de la figer d'abord (le codeur a dû monter un worktree) · quelques minutes · C03 lot 2
- C04 : le critère FPS citait 453,5 (C02, anciens arbres) alors que le pack à 800 triangles était à 302 ; une spec cite la dernière mesure FPS, pas celle du chantier précédent.
- 2026-10-25 C05 : les agents du dépôt (codeur, auditeur, relecteur) ne sont pas chargés dans une session de projet cloud ; relais par general-purpose (Sonnet) qui lit .claude/agents/<rôle>.md. Et la CI s'est arrêtée net faute de minutes Actions (dépôt privé), dépôt rendu public.
- 2026-10-25 C05 lot 1 : une spec de physique doit marquer « signe à vérifier au lot » quand elle prescrit un facteur non testé contre le modèle (restitution ×1,15 faisait rouler le drive moins loin).
- 2026-10-25 : les agents du dépôt (codeur, auditeur) n'étaient pas chargés dans une session de thread ; délégués en general-purpose avec le fichier de rôle à lire (C06).
- 2026-10-25 : panne du téléchargement .NET de Microsoft (HTTP 409) ; repli apt ajouté à ci.yml (PR #51).
- Reprendre une branche déjà fusionnée depuis main demande un push --force-with-lease (consigne de la session cloud), contraire à la règle « jamais de --force » : nommer une nouvelle branche pour la suite.
- 2026-10-26 C08 : le brief d'audit doit citer le chemin des fichiers de données du chantier (le trou de test), l'auditeur a dû le chercher.
