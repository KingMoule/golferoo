---
name: passation
description: Mettre à jour docs/handoff.md et docs/chantiers.md pour qu'une nouvelle session reprenne sans rien relire. Pour /passation, « sauvegarde où on en est », la fin d'une session ou d'une vague, un /clear, un changement de compte Claude ou la reprise après un tel changement.
---

# Passation

`docs/handoff.md` est un tableau blanc, pas un journal : on réécrit en place, on n'ajoute pas à la suite. 40 lignes au plus.

Ce qui n'est pas dans le dépôt n'existe pas pour la session suivante : un autre compte Claude ne voit ni les fils, ni le projet, ni la mémoire de celui-ci. Les règles que Guillaume a posées dans les instructions du projet (pas dans `CLAUDE.md`) se recopient dans la section « Règles de projet » du handoff.

## 1. Lire
Lis `docs/handoff.md` et `docs/chantiers.md` en entier. Vérifie ce que tu reportes sur GitHub plutôt que sur parole : une PR « en cours » l'est-elle encore (`gh pr view`, ou `list_pull_requests`) ? une branche existe-t-elle encore ? la CI est-elle verte ?

## 2. Réécrire `docs/handoff.md`
```
Mis à jour : <date UTC réelle, `date -u +%F`>

## État
<une ligne : où en est le projet maintenant>

## En cours
- <chantier, branche, PR, ce qui reste> [guillaume|agent]

## Reporté
<ce qu'on a décidé de reprendre plus tard>

## Règles de projet
<règles posées hors `CLAUDE.md` : fusion, assets, agents, etc.>

## Décisions récentes
- <décision encore contestable, qui l'a prise, pourquoi en une ligne>
- Approches abandonnées : <quoi et pourquoi, si une future session risque de les retenter>

## Prochaine étape
<une action précise, ou « EN ATTENTE de Guillaume : <quoi> », ou « Choisir dans docs/chantiers.md »>
```
Pour une PR en pause, note sa branche, son dernier commit, pourquoi elle attend et ce qui la débloque. Une décision devenue règle permanente monte dans `CLAUDE.md` (avec l'accord de Guillaume) ; une décision close sort du handoff.

## 3. Mettre à jour `docs/chantiers.md`
Une ligne par chantier dans le tableau ; statut parmi `à faire`, `en cours`, `bloqué`, `livré`. Les chantiers livrés depuis plus d'une vague descendent dans la section Archive en une ligne.

## 4. Vérifier
`wc -l docs/handoff.md` (40 au plus). Commit sur la branche courante si elle n'est pas `main` ; sinon, signale les fichiers à committer. Pour un changement de compte, la passation doit être **fusionnée dans `main`** avant de partir, sinon l'autre compte ne la voit pas.

## Reprendre sur un autre compte
1. Cloner le dépôt et lire `CLAUDE.md`, puis `docs/handoff.md` (le seul à lire en entier), puis la spec du chantier en cours.
2. Contrôler l'état réel : `list_pull_requests` ouvertes, CI du dernier commit de `main`, branches citées par le handoff. Si le handoff et GitHub divergent, GitHub a raison : corrige le handoff.
3. Les PR en pause se reprennent depuis leur branche (ne pas en rouvrir une neuve) ; une PR déjà fusionnée repart d'une nouvelle branche.
4. Rappeler à Guillaume les points « [guillaume] » du handoff, puis exécuter la « Prochaine étape ».
