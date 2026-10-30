---
name: deleguer
description: Préparer et lancer un chantier Golferoo confié à un agent (codeur, auditeur, relecteur) avec un brief d'une page, un niveau de contrôle et un contexte cité plutôt que recopié. À utiliser par l'orchestrateur avant chaque délégation.
---

# Déléguer un chantier

But : un agent qui démarre à froid reçoit exactement ce qu'il lui faut et rend un résultat court. `CLAUDE.md` est déjà chargé chez lui : ne le recopie pas.

## Faut-il déléguer ?
- Oui : implémentation de plus de quelques lignes, sortie verbeuse (tests, journaux), audit à regard neuf.
- Non : changement décrit en une phrase, séquence de trois ou quatre commandes, travail qui dépend de toute la conversation. Un agent coûte un démarrage à froid (souvent 20 000 à 60 000 tokens).

## Avant le brief
1. Lis `docs/chantiers.md` et, si le chantier est cadré, son fichier `specs/<id>.md`. Rien d'autre par défaut.
2. Situe le code avec l'agent `Explore` (Haiku) si tu ne sais pas où il vit. Cite des noms de fonctions ou de fichiers, jamais des numéros de ligne.
3. Fixe le niveau de contrôle :
   - **complet** : physique et simulation, état de partie et score, sauvegarde et chargement, tout ce qui perd des données ou fausse un résultat.
   - **léger** : écrans de consultation, statistiques, options, outils de développement.
   - **aucun** : libellés, documentation, style. CI verte et relecture de la PR par Guillaume.
4. Parallélisme : un seul codeur par zone de code. Deux codeurs en même temps seulement sur des zones disjointes, chacun avec `isolation: "worktree"`.
5. Ce que le brief doit nommer : les fichiers de données du chantier ; pour un critère « inchangé » ou « au bit près », la référence à figer sur `main` d'abord (et le petit test de relevé à commiter) ; pour un audit, le diff `git diff origin/main...origin/<branche>` (jamais `<sha>^1`, faux si la tête est une fusion de main).

## Le brief (une page au plus)
```
CHANTIER : <id> — <titre>
RÔLE : codeur | auditeur | relecteur
NIVEAU DE CONTRÔLE : complet | léger | aucun
OBJECTIF : <une phrase, résultat observable>
DÉCISIONS DE GUILLAUME : <ce qui est tranché, citations courtes>
PÉRIMÈTRE : <fichiers, fonctions>
HORS PÉRIMÈTRE : <ce qu'on ne touche pas>
CRITÈRES D'ACCEPTATION : <liste numérotée, chacun vérifiable par une commande>
TESTS : <tests à écrire, commande de test ciblée>
BRANCHE : <claude/<id>-<slug>> ; PR vers main : oui | non
À LIRE : <specs/<id>.md, sections précises>
RENDU : 30 lignes au plus (statut, critères, commandes et résultats, écarts, questions, friction)
```

## Lancer
- Agent du dépôt (`codeur`, `auditeur`, `relecteur`) ; ne surcharge le modèle que pour une raison écrite dans le brief.
- Un codeur à reprendre pour un correctif se relance par `SendMessage`, pas par un agent neuf qui relirait tout.
- N'attends pas en sondant : l'agent rend la main quand il a fini.

## Après
1. Lis le rendu court. Ouvre le diff seulement si un critère est non vérifié, si la CI est rouge ou s'il y a un écart au brief.
2. Niveau complet ou léger : lance l'`auditeur` avec le même brief et le lien de la PR.
3. Mets à jour `docs/chantiers.md` en une ligne (statut, PR, constat ouvert).
4. Une ligne `Friction :` autre que « aucune », ou un aller-retour que tu as dû faire toi-même : ajoute-la à `docs/lecons.md` par `echo "- ..." >> docs/lecons.md`, sans relire le fichier.
5. Agent coupé (limite d'usage) : `SendMessage` le reprend ; s'il refuse, un nouvel agent reprend dans son worktree, où le travail non commité est resté. Un agent qui attend une fusion se termine plutôt que de boucler sur des minuteurs ; on le relance après.
6. Le `relecteur` passe à la fin d'une vague ou avant une publication, pas à chaque chantier ; suis-le de `/ameliorer`.
