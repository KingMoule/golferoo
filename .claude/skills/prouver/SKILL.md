---
name: prouver
description: Vérifier contre de vraies sources les affirmations factuelles d'une réponse précédente. Pour /prouver, « prouve-le », « source ? », « tu l'as inventé ? ». Rend un verdict confirmé, faux ou non vérifié par affirmation, puis corrige.
---

# Prouver

1. Extrais les affirmations vérifiables du passage contesté : chiffres, noms, chemins, comportements, attributions. Mets à part les opinions annoncées comme telles.
2. Vérifie chacune à la bonne source :
   - fait du projet : fichiers, `git log`, specs, handoff ;
   - comportement : lis le code ou lance une démonstration sans effet de bord ;
   - fait externe : documentation officielle ou source primaire récente.
3. Si deux sources se contredisent, tranche par la date et l'artefact réel. La mémoire peut orienter la recherche, jamais confirmer.
4. Une ligne par affirmation : **Confirmé** (preuve), **Faux** (fait correct et source), **Non vérifié** (ce qui manque ; l'affirmation est retirée).
5. Si une affirmation change, réécris brièvement la réponse corrigée et signale les décisions touchées.

L'absence de contradiction n'est pas une confirmation. Ne t'arrête pas tant qu'une source disponible n'a pas été consultée.

_Adapté de `prove` de [claude-harness-toolbox](https://github.com/dtiger1889-ops/claude-harness-toolbox) (MIT)._
