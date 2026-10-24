---
name: ameliorer
description: Revue périodique du workflow Golferoo à partir de docs/lecons.md - regrouper les frictions, corriger celles qui reviennent dans la bonne couche, élaguer ce qui ne sert plus, et proposer le tout en PR. Pour /ameliorer, la fin d'une vague, ou quand le reçu de démarrage le suggère.
---

# Améliorer le workflow

Une revue rare et groupée vaut mieux qu'une règle ajoutée à chaque incident : chaque ligne de `CLAUDE.md` est payée à chaque tour, chaque description de skill à chaque session.

## 1. Rassembler (lecture seulement de ces sources)
- `docs/lecons.md`, section « À traiter ».
- Les rendus récents qui portent une ligne `Friction :` et qui n'ont pas encore été recopiés.
- Si Guillaume colle la sortie de `/usage` (part des skills, agents et MCP, recommandations) ou de `/insights`, ajoute-la : c'est la seule mesure objective du coût.
- `bash .claude/hooks/verif-budgets.sh` pour les budgets de taille.

## 2. Trier par famille
Regroupe les leçons par famille et par cause, pas par incident. Pour chaque groupe :
- **Une seule occurrence** : on la garde en attente, sauf si elle est grave (perte de données, push dangereux, fuite de secret).
- **Deux occurrences ou plus**, ou une grave : on corrige.
- **Bruit** (cas unique, cause externe, déjà réglé) : on supprime.

## 3. Corriger dans la couche la plus étroite
Même échelle que `/prevenir` : hook ou `permissions.deny` pour un motif mécanique ; skill ou définition d'agent pour une procédure ; `CLAUDE.md` seulement si chaque session en a besoin ; brief type de `deleguer` pour ce qui manque aux agents. Préfère modifier ou remplacer une règle existante plutôt qu'en ajouter une.

## 4. Élaguer (obligatoire à chaque revue)
- Une règle de `CLAUDE.md` que Claude respecte sans elle, ou devenue un hook : retire-la.
- Un skill ou un agent pas utilisé depuis deux revues : propose de le retirer.
- Les budgets de `verif-budgets.sh` doivent repasser au vert.
Une revue qui n'ajoute que des lignes est suspecte : vise un solde de lignes nul ou négatif dans `CLAUDE.md`.

## 5. Proposer
- Une branche `claude/ameliorer-<date>`, une PR vers `main`. Guillaume fusionne : aucune règle ne change sans lui.
- Dans la PR : chaque changement, la famille de leçons qu'il traite, le nombre d'occurrences, ce qui a été élagué, le solde de lignes de `CLAUDE.md`.
- Vide de `docs/lecons.md` les leçons traitées ou supprimées ; garde celles en attente.
- Ajoute une ligne datée au « Journal des revues » de `docs/workflow.md`.
- Au début de la revue suivante, vérifie si les leçons de la revue précédente sont revenues : si oui, la correction était dans la mauvaise couche.
