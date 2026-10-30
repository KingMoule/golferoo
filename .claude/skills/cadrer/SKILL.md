---
name: cadrer
description: Interroger Guillaume avant un nouveau chantier ou une nouvelle fonctionnalité, puis écrire la spec specs/<id>.md avec critères d'acceptation et clôture. Pour /cadrer, « interroge-moi », ou le premier build d'une fonctionnalité. À sauter pour un correctif, une petite modification ou une spec déjà validée.
---

# Cadrer un chantier

## 1. Lire d'abord
Lis `docs/chantiers.md`, la spec existante s'il y en a une, et ce que Guillaume a déjà dit dans la conversation. Ce qui est déjà répondu est tranché : ne le redemande pas.

## 2. Poser seulement les questions qui changent le résultat
Par rondes courtes et numérotées, avec l'outil de questions structurées s'il est disponible. Commence par le but, l'utilisateur, la plateforme, le périmètre et ce qui prouve que c'est réussi ; creuse ensuite ce que les réponses révèlent. Pour Golferoo, pense à : réalisme physique attendu contre jouabilité, plateforme et contrôles, performance cible, ce qui est hors périmètre.

Si toutes les réponses plausibles mènent au même build, ne pose pas la question. Si Guillaume dit d'avancer, avance et note les hypothèses.

## 3. Écrire la spec
Un critère cite la dernière mesure (FPS), marque « signe à vérifier » un facteur de physique non testé contre le modèle, dit « fumée Godot » quand il faut le moteur, vérifie un évènement plutôt qu'un compteur, teste une règle de calendrier avec la vraie date de `Calendar`, et ne prescrit pas un chiffre physique sans l'avoir mesuré (« le fer 5 roule autant que le bois 3 » était faux).

`specs/<id>-<slug>.md`, une page autant que possible :
```
# <Titre>
Statut : proposé | validé | livré
Créé : <date>

## But
## Périmètre et hors périmètre
## Conception
<fichiers, interfaces, données ; noms stables plutôt que numéros de ligne>
## Choix à trancher par Guillaume
<pour chacun : 2 ou 3 options, gains, pertes, recommandation et la preuve sur laquelle elle repose>
## Critères d'acceptation
<liste numérotée ; chaque critère vérifiable par une commande ou un test>
## Niveau de contrôle
complet | léger | aucun
## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
```

Ajoute la ligne du chantier dans `docs/chantiers.md`. Une spec n'autorise pas le build : Guillaume la valide d'abord, sauf s'il a déjà dit d'avancer.
