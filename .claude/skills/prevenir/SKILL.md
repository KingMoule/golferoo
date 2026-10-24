---
name: prevenir
description: Corriger une erreur répétée ou déjà documentée, puis renforcer le flux qui l'a laissée passer pour que la règle soit lue ou imposée au bon moment. Pour /prevenir, « on en a déjà parlé », « c'est documenté », « pourquoi tu refais ça ».
---

# Prévenir la récidive

1. **Corrige l'erreur immédiate** dans le périmètre autorisé. La prévention ne retarde pas la correction.
2. **Retrouve la règle existante** (`CLAUDE.md`, skills, agents, specs, handoff) et cite-la. Si elle n'a jamais été écrite, dis-le.
3. **Diagnostique le chemin raté** : règle absente, enfouie, dans la mauvaise couche, jamais lue par ce point d'entrée (un agent qui ne charge pas ce skill, par exemple), ou lue mais pas imposée. Sépare le prouvé de l'hypothèse.
4. **Change la couche la plus étroite qui sera sûrement atteinte** :
   - motif mécanique : un hook testé ou une règle `permissions.deny` ;
   - procédure : le skill ou la définition d'agent concernés ;
   - règle valable pour toute session : `CLAUDE.md`, seulement si chaque session en a besoin (il coûte à chaque tour) ;
   - état ou décision en cours : `docs/handoff.md`.
5. **Vérifie la portée** : le flux fautif charge-t-il vraiment le changement ? Pour un hook, teste un cas fautif et un cas légitime.
6. **Journalise** une ligne dans `docs/lecons.md` (famille `règle-ignorée`) : `/ameliorer` verra si la correction a tenu.
7. **Rends brièvement** : correction faite, règle et trou trouvés, changement durable et sa vérification.

Ne crée pas de modification juste pour dire « c'est prévenu ». Pour un même type d'erreur qui revient par plusieurs entrées, renforce la famille, pas un cas à la fois.

_Adapté de `prevent` de [claude-harness-toolbox](https://github.com/dtiger1889-ops/claude-harness-toolbox) (MIT)._
