# Leçons en attente

Journal des frictions du workflow, une ligne chacune, en attendant la prochaine revue `/ameliorer`.
Une friction : une règle ignorée, un aller-retour évitable, un agent qui a relu ce qu'il aurait pu recevoir, une sortie qui a noyé le contexte, un audit qui a raté quelque chose, une consigne qui a coûté plus qu'elle n'a rapporté.

Format : `- AAAA-MM-JJ · famille · ce qui s'est passé · coût estimé · source (chantier, PR)`
Familles : `contexte`, `délégation`, `vérification`, `règle-ignorée`, `outil`, `spec`, `autre`.

Ajouter sans relire tout le fichier : `echo "- ..." >> docs/lecons.md`. `/ameliorer` vide la liste une fois les leçons traitées.

## À traiter
- 2026-10-28 · délégation · deux lots parallèles (C12 lots 3 et 4) se gênent sur les données partagées : le lot qui mesure se fabrique ses propres données plutôt que d'attendre celles de l'autre · en attente (une occurrence) · C12
- 2026-10-29 · outil · la modification de `.claude/settings.json` par la session (allow-list des commandes d'agents) est refusée par le mode auto : à faire par Guillaume s'il la veut · en attente · /ameliorer du 2026-10-29
