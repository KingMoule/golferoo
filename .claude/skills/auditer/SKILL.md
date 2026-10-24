---
name: auditer
description: Mener l'audit indépendant d'un chantier Golferoo au niveau complet ou léger donné par le brief, en réutilisant la CI et en concentrant l'effort sur la preuve indépendante. À utiliser par l'agent auditeur.
---

# Auditer un chantier

Tu n'écris rien dans le dépôt. Ton but : trouver ce que le codeur n'a pas testé, pas refaire ce que la CI a déjà prouvé.

## 1. Le niveau vient du brief
| Niveau | Ton travail |
|---|---|
| **complet** | CI verte, puis tout le §2 |
| **léger** | CI verte, puis §2 points 1 et 4, plus les invariants touchés par le diff |
| **aucun** | pas d'audit |

Sans niveau dans le brief, prends « complet ».

## 2. Prouver toi-même
1. Chaque critère d'acceptation, vérifié par **ta propre** commande ou ton propre script, pas en relisant le test du codeur.
2. Le contrôle négatif : le test du chantier échoue sur `main` (`git stash` interdit ; utilise `git worktree add /tmp/audit-main origin/main`).
3. Des scénarios que le brief n'a pas prévus : valeurs limites (vent nul ou extrême, pente, balle hors limites, distance nulle), entrées répétées ou simultanées, pause et reprise, rechargement d'une sauvegarde, très petit écran.
4. Non-régression : la suite de tests ciblée du domaine touché.

Filtre toutes les sorties. Une suite complète se lit en CI (`gh pr checks`, `gh run view --log-failed | tail -n 60`), elle ne se relance pas dans ta session.

## 3. Rendre (30 lignes au plus)
- Verdict : CONFORME, CONFORME AVEC RÉSERVES ou NON CONFORME.
- Critère par critère : vérifié comment, résultat.
- Constats : gravité, scénario, preuve (commande et sortie utile).
- Ce que tu as réutilisé de la CI (commit) et ce que tu n'as pas pu vérifier.
