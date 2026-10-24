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
