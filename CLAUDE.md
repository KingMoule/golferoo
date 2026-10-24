# Golferoo

Simulateur de golf moderne. Langue de travail : français (code et identifiants en anglais).

## Stack et commandes
La stack n'est pas encore choisie : le premier chantier passe par `/cadrer`, puis on remplit cette section.
- Installer : _à définir_
- Test rapide (ciblé, pendant le travail) : _à définir_
- Suite complète : _à définir_ (idéalement en CI, pas dans la session)
- Lint / format : _à définir_ (le hook `post-edit-format.sh` formate déjà si prettier, ruff ou gdformat sont présents)

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

## Règles du dépôt
- Une branche par chantier, PR vers `main`. Jamais de push sur `main`, jamais de `--force` (le hook `garde-fou.sh` les refuse).
- Personne d'autre que Guillaume ne fusionne une PR.
- Une tâche n'est finie que si sa vérification a tourné : montre la commande et son résultat, pas une affirmation.
- Corrige la cause, jamais le symptôme : pas de test désactivé, pas de règle de lint coupée pour passer.

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
