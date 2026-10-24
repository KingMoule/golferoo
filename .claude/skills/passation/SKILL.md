---
name: passation
description: Mettre à jour docs/handoff.md et docs/chantiers.md pour qu'une nouvelle session reprenne sans rien relire. Pour /passation, « sauvegarde où on en est », la fin d'une session ou d'une vague, ou avant un /clear.
---

# Passation

`docs/handoff.md` est un tableau blanc, pas un journal : on réécrit en place, on n'ajoute pas à la suite. 40 lignes au plus.

## 1. Lire
Lis `docs/handoff.md` et `docs/chantiers.md` en entier. Vérifie ce que tu reportes : une PR « en cours » l'est-elle encore (`gh pr view`) ? une branche existe-t-elle encore ?

## 2. Réécrire `docs/handoff.md`
```
Mis à jour : <date UTC réelle, `date -u +%F`>

## État
<une ligne : où en est le projet maintenant>

## En cours
- <chantier, branche, PR, ce qui reste> [guillaume|agent]

## Décisions récentes
- <décision encore contestable, qui l'a prise, pourquoi en une ligne>
- Approches abandonnées : <quoi et pourquoi, si une future session risque de les retenter>

## Prochaine étape
<une action précise, ou « EN ATTENTE de Guillaume : <quoi> », ou « Choisir dans docs/chantiers.md »>
```
Une décision devenue règle permanente monte dans `CLAUDE.md` (avec l'accord de Guillaume) ; une décision close sort du handoff.

## 3. Mettre à jour `docs/chantiers.md`
Une ligne par chantier dans le tableau ; statut parmi `à faire`, `en cours`, `bloqué`, `livré`. Les chantiers livrés depuis plus d'une vague descendent dans la section Archive en une ligne.

## 4. Vérifier
`wc -l docs/handoff.md` (40 au plus). Commit sur la branche courante si elle n'est pas `main` ; sinon, signale les fichiers à committer.
