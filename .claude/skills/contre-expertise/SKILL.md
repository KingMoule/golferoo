---
name: contre-expertise
description: Une passe adverse sur un plan, une spec, une affirmation ou un choix technique, qui suppose qu'il est faux et cherche la faille fatale. Seulement pour /contre-expertise, « challenge ça », « pousse contre », « stress-test ».
---

# Contre-expertise

1. Identifie la cible (passage, fichier, réponse précédente). Demande une seule fois si c'est ambigu.
2. Lis l'artefact cité et l'état du projet qui le concerne (`docs/handoff.md`, la spec). Les décisions déjà prises font partie de la revue.
3. Essaie de la réfuter, puis rends :
   - **Objection la plus forte** : l'échec plausible le plus coûteux, son mécanisme, la preuve.
   - **Hypothèse critique** : ce qui doit tenir pour que la cible marche.
   - **Explication alternative** : seulement si elle est appuyée ; n'invente pas de leurre.
   - **Réfutation la moins chère** : une vérification concrète qui trancherait.
   - **Verdict** : tient, à réviser ou à abandonner, avec la correction la plus importante.
4. Si la cible est un fichier écrit par cette session et que la correction est dans le périmètre, applique-la et dis-le.

Une seule passe. Sépare preuve et hypothèse, et dis-le quand la cible tient.

_Adapté de `redteam` de [claude-harness-toolbox](https://github.com/dtiger1889-ops/claude-harness-toolbox) (MIT)._
