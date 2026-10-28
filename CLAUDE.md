# Golferoo

Simulateur de golf moderne. Langue de travail : français (code et identifiants en anglais).

## Stack et commandes
Godot 4.7 (.NET) pour le rendu, l'UI et les outils ; toute la simulation dans `Golferoo.Sim`, bibliothèque C# pure sans dépendance à Godot (`docs/adr/0001-moteur.md`). Commandes vérifiées aux lots 1 et 8 de C01 (PR #6, #14).
- Installer : SDK .NET 10 (conteneur cloud : `apt-get install dotnet-sdk-10.0`) et Godot 4.7.2 .NET ; `dotnet restore Golferoo.sln`
- Test rapide (ciblé, pendant le travail) : `dotnet test tests/Golferoo.Sim.Tests --filter <Nom>`
- Suite complète : `dotnet test Golferoo.sln` ; banc `dotnet run -c Release --project tests/Golferoo.Sim.Bench` ; Godot (en CI, `.github/workflows/ci.yml`) : `dotnet build game/Golferoo.Game.csproj`, `godot --headless --path game --import`, puis `godot --headless --path game res://tests/Smoke.tscn` qui doit écrire `SMOKE OK`
- Captures : `godot --path game res://tools/Captures.tscn -- --out dossier [--factor n] [--thumbs] [--full]` (en CI : lignes `THUMB`/`FULL` en JPEG base64 dans le journal du job « Captures », lisibles sans télécharger l'artefact) ; export Windows : job CI « Export Windows », artefact `golferoo-windows` (`Mesurer FPS.bat` = `-- --fps-bench`)
- Assets : `pip install bpy==5.2.3` (Python 3.13) puis `python -I tools/blender/tree_lowpoly.py` régénère `game/assets/trees/tree.glb`
- Lint / format : `dotnet format Golferoo.sln --verify-no-changes` (le hook `post-edit-format.sh` formate les `.cs` si `dotnet` est présent)
- Jamais de `using Godot` dans `src/Golferoo.Sim/`. Aucun fichier d'OpenSGolf ni d'EA n'est copié dans le dépôt : leurs docs et chiffres se citent.

## Rôles et modèles
| Rôle | Où | Modèle / effort | Quand |
|---|---|---|---|
| Orchestrateur | session principale | Opus, medium | cadre, découpe, délègue, tranche, intègre |
| Codeur | agent `codeur` | Sonnet, medium | un chantier délimité par un brief |
| Auditeur | agent `auditeur` | Sonnet, high | chantiers à contrôle *complet* ou *léger* |
| Relecteur externe | agent `relecteur` | Opus, high | par vague ou avant une publication, jamais par chantier |
| Exploration | agent `Explore` (surchargé) | Haiku | « où est X », inventaire, résumé de journaux |

- L'orchestrateur ne code pas un chantier de plus de quelques lignes : il délègue avec le skill `deleguer`.
- Un changement que tu décris en une phrase se fait directement, sans plan ni agent.
- Architecture, règle de physique du jeu, choix difficile : `ultrathink` ou `/effort xhigh` pour ce tour, pas en permanence.

## Simplicité
Less is more. You see elegance in simplicity. You favour concise, clean solutions over those that are complex and comprehensive for risk of over-engineering. Upon work completion, take pause to critically review your work: if complexity has crept in, address the issue and suggest a small change to our workflow to help avoid this in the future.

## Règles du dépôt
- Une branche par chantier, PR vers `main`. Jamais de push sur `main`, jamais de `--force` (le hook `garde-fou.sh` les refuse).
- La session principale fusionne elle-même une PR prête (`merge_pull_request`, méthode *merge*, avec `expectedHeadSha`) : les quatre jobs de CI verts sur ce commit, audit prévu par la spec sans bloquant, aucun commentaire de revue ouvert. Exception : une PR qui touche le workflow (`CLAUDE.md`, `.claude/`, `docs/workflow.md`) attend Guillaume.
- Une tâche n'est finie que si sa vérification a tourné : montre la commande et son résultat, pas une affirmation.
- Corrige la cause, jamais le symptôme : pas de test désactivé, pas de règle de lint coupée pour passer.
- Tout nouvel état de la sim entre dans `World.Checksum` et dans `World.Save`, avec un cas `SaveTests` qui échoue si on l'oublie.

## Économie de tokens
- Lis le moins possible : cherche (`Grep`, `Glob`) avant de lire, lis des plages plutôt que des fichiers entiers.
- Sortie de commande longue : filtre-la (`| tail -n 40`, `grep -E 'FAIL|Error'`) ou délègue-la à un agent qui rend un résumé.
- Un agent coûte un démarrage à froid : ne délègue que si le travail inline dépasserait environ 30 000 tokens ou doit être isolé.
- Cite les fichiers au lieu de les recopier dans un brief ; un brief tient sur une page.
- Après deux corrections ratées sur le même point : `/clear` et un meilleur prompt plutôt qu'une troisième.
- `/clear` entre deux sujets sans rapport ; `/btw` pour une question qui ne doit pas rester en contexte.

## État du projet
- `docs/handoff.md` : où on en est (40 lignes au plus). À lire avant de reprendre un chantier, inutile si la demande nomme déjà la tâche.
- `docs/chantiers.md` : registre vivant des chantiers.
- `specs/` : un fichier par chantier cadré, avec ses critères d'acceptation.
- `docs/lecons.md` : frictions du workflow, une ligne chacune (`echo "- ..." >> docs/lecons.md`, sans relire). `/ameliorer` les traite par lot.
- `docs/workflow.md` : pourquoi le workflow est ainsi (à lire seulement pour le modifier).

## Instructions de compaction
Conserve : la liste des fichiers modifiés, la branche et la PR en cours, les commandes de test et leur dernier résultat, les décisions de Guillaume, les critères d'acceptation non encore vérifiés.
