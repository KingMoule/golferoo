# Golfeurs animés (P2)
Statut : validé
Créé : 2026-10-28

## But
Les golfeurs sont aujourd'hui des silhouettes procédurales dans un `MultiMesh` (`ActorLayer`, `ProceduralMeshes.Golfer`), sans animation. Or la boucle du jeu se lit sur eux : ils marchent, ils frappent, ils réagissent. C23 les remplace par le golfeur de Guillaume, animé, et mesure ce que coûte le nombre (preuve P2 de `docs/faisabilite-features.md`, risque R2).

Ce qui prouve la réussite : en jeu, les golfeurs proches marchent et frappent avec le modèle de Guillaume, la balle part à l'impact du swing ; la mesure de Guillaume tient 144 FPS avec 100 golfeurs animés et 60 FPS avec 200.

## Source
Dossier du projet, `c23/` : `golfer_polo_v4.blend`, `golfer_polo_v4.glb` (sans squelette) et `golfer_polo_v4_rigged.glb`. Ce dernier : un maillage d'environ 2 100 triangles en quatre parties, un squelette de 21 os (`root`, `hips`, `spine`, `chest`, bras, mains, `club`, `neck`, `head`, jambes, pieds) et une seule animation, `golf_swing_2s`. **Pas de marche** : elle viendra de Guillaume plus tard, avec le putt, l'attente et les réactions.

## Décisions de Guillaume (cadrage du 2026-10-28, spec validée le même jour)
1. Marche provisoire **générée par un script Blender** sur son squelette ; remplacée par la sienne dès qu'elle arrive, sans toucher au code.
2. Cible : **144 FPS jusqu'à 100 golfeurs animés à l'écran, 60 FPS à 200** (RTX 2080, 1080p), mesurée par Guillaume.
3. Golfeurs animés jusqu'à environ **150 m** de la caméra ; au-delà, les silhouettes actuelles.
4. La balle part à **l'image de l'impact** du swing ; le chip est le swing raccourci ; le putt garde ce mouvement, ralenti et réduit, en attendant son animation.
5. Un seul modèle de club pour l'alpha.
6. Variété : couleur du polo selon le profil de golfeur (C09), quelques teintes de peau et de pantalon, taille à ±5 %.

## Périmètre et hors périmètre
**Dans le périmètre**
- Filière `bpy` : import, échelle et orientation normalisées, marche générée, export d'un `.glb` du jeu avec ses animations.
- Golfeur animé en jeu : marche, attente (pose de repos), swing, chip, putt provisoire ; synchronisation de l'impact avec le coup de la sim.
- Mélange proche (animé) et lointain (silhouette) ; banc de 200 golfeurs ; repli si la mesure le demande.
- Variété de teintes et de tailles.

**Hors périmètre, et où ça ira**
- Putt, attente, joie et déception dessinés par Guillaume : lot 2 de ce chantier dès réception ; réactions déclenchées par l'humeur : C16.
- Plusieurs clubs (putter, bois) : plus tard. Voiturettes : hors alpha.

## Conception
**Filière** (`tools/blender/golfer_anim.py`, sur le modèle des scripts existants ; `pip install bpy==5.2.3`, Python 3.13, `python -I`)
- Entrée : le `.blend` ou le `.glb` riggé de Guillaume (copié dans `tools/blender/src/` ou lu depuis un chemin passé en argument). Sortie : `game/assets/golfers/golfer.glb`.
- Normalise l'échelle (1,75 m), l'axe avant et l'origine aux pieds ; vérifie le nombre d'os et de triangles.
- Génère la boucle de marche (environ 1 s, 1,3 m par cycle, à caler sur la vitesse de marche de la sim) par images clés sinusoïdales sur hanches, cuisses, tibias, bras et colonne ; une pose de repos ; garde `golf_swing_2s`. Si une animation `walk` existe déjà dans le fichier source, elle est gardée telle quelle : la marche de Guillaume remplace la générée sans changer le script.
- Repère l'**image de l'impact** du swing (le club au plus bas, vitesse maximale de l'os `club`) et l'écrit dans les métadonnées du `.glb` (extras) pour le jeu.

**Jeu** (`game/src/Rendering/`)
- `GolferRig` : un `Skeleton3D` et un `AnimationPlayer` (ou `AnimationTree` simple) par golfeur proche, dans un **pool** borné ; état de la sim → animation (marche pour Arriving, WalkingToBall, Exiting, ToNextTee, Roaming ; repos pour WaitingTurn, AwaitingShot, Done ; swing ou chip pour Swinging ; putt pour Putting ; fin du geste pendant BallInFlight). Vitesse de la marche réglée sur la vitesse réelle du golfeur (pas de patinage).
- `ActorLayer` garde le `MultiMesh` de silhouettes pour les golfeurs au-delà de 150 m (ou au-delà du pool) ; un golfeur passe de l'un à l'autre sans saut visible.
- Teintes par `instance_shader_parameter` (polo par profil, peau, pantalon) et échelle ±5 % tirées de l'identifiant du golfeur (stables d'une partie à l'autre).

**Synchronisation de l'impact**
- La sim tient un golfeur en `Swinging` pendant `SwingTicksCount` (1 s) avant de lancer la balle. Ce délai devient la durée jusqu'à l'impact du swing, arrondie au tick : 23 ticks (impact à l'image 35, 1,13 s). C'est une constante de la sim, qui ne lit aucun fichier ; la fumée échoue si l'impact écrit dans le `.glb` s'en écarte. C'est un changement de la sim : sommes de contrôle et bancs à revérifier, aucun nouvel état.
- Chip : même swing joué plus vite et avec moins d'amplitude (mélange avec la pose de repos), impact au même tick. Putt provisoire : swing à 40 % d'amplitude.

**Banc** : `--fps-bench-golfers 200` (et 100) dans `FpsBench` : 200 golfeurs qui marchent et frappent sur « Le neuf », caméra fixe qui en voit le plus possible ; `Mesurer FPS.bat` gagne ces deux mesures. **Repli** : si la mesure de Guillaume manque la cible, lot 3 passe aux animations cuites en texture lues par le shader d'un `MultiMesh`.

## Lots
| Lot | Contenu | Contrôle |
|---|---|---|
| 1 | Filière `bpy`, `golfer.glb` avec marche générée et swing, `GolferRig` et pool, synchronisation de l'impact, chip et putt provisoires, bascule proche et lointain, banc 100 et 200 ; captures `c23-marche` et `c23-swing` | complet (sim : délai du swing), léger (rendu) |
| 2 | Animations de Guillaume (marche, putt, attente, réactions) dès réception ; variété de teintes et de tailles ; capture `c23-variete` | léger |
| 3 | Repli par animations cuites en texture, seulement si la mesure le demande | léger |

## Choix à trancher par Guillaume
Aucun. Une mesure de sa part à la fin du lot 1 (`Mesurer FPS.bat`).

## Critères d'acceptation
1. `python -I tools/blender/golfer_anim.py` régénère `game/assets/golfers/golfer.glb` à l'identique (même empreinte) ; le fichier contient `walk`, `idle` et `golf_swing_2s`, 21 os, moins de 2 500 triangles, l'image d'impact en métadonnée.
2. Dans la fumée Godot, un golfeur proche joue `walk` en marchant, `idle` en attendant, le swing en `Swinging`, et l'animation suit l'état quand il change.
3. La balle quitte le sol au tick où le swing atteint son impact (fumée : écart d'au plus un tick entre l'image d'impact jouée et le départ de la balle).
4. Test de la sim : un golfeur reste en `Swinging` exactement `World.SwingTicksCount` ticks, et la fumée vérifie que cette constante tombe sur l'image d'impact du `.glb` à un tick près ; même graine, même somme de contrôle ; `SaveTests` passe.
5. Le pool ne dépasse jamais sa borne ; au-delà de 150 m ou au-delà du pool, le golfeur est une silhouette du `MultiMesh` ; la bascule ne fait pas sauter sa position (fumée).
6. La vitesse de la marche suit la vitesse du golfeur à 10 % près (pas de patinage visible ; fumée sur la distance parcourue par cycle).
7. Les captures `c23-marche` et `c23-swing` montrent le modèle de Guillaume en marche et au sommet du swing.
8. Mesure de Guillaume : 144 FPS ou plus avec 100 golfeurs animés, 60 FPS ou plus avec 200 (`Mesurer FPS.bat`).
9. Le banc de la sim reste dans ses seuils (tick moyen sous 2 ms, 99e centile sous 5 ms) ; les quatre jobs de CI sont verts.

## Niveau de contrôle
complet pour le délai du swing dans la sim, léger pour le rendu.

## Clôture
- [ ] Critères vérifiés
- [ ] Statut passé à « livré », ligne mise à jour dans docs/chantiers.md
