# Workflow Claude Code de Golferoo : choix et justifications

Ce document explique pourquoi la configuration est ainsi. Il n'est pas chargé automatiquement : on le lit pour modifier le workflow, pas pour travailler. Prix et comportements vérifiés dans la documentation Anthropic le 2026-10-24.

## Le principe directeur
La documentation d'Anthropic part d'une contrainte : la fenêtre de contexte se remplit vite, et la qualité baisse quand elle se remplit. Presque tout le reste en découle. On garde donc le contexte de l'orchestrateur petit, on envoie le travail volumineux dans des agents à contexte séparé qui rendent un résumé, et on met les modèles chers seulement là où une décision coûte cher.

Deuxième principe : une vérification que Claude peut lancer lui-même (test, build, capture) vaut plus que toute consigne. Une tâche n'est finie que si sa preuve a tourné.

## Rôles et modèles
| Rôle | Modèle, effort | Pourquoi |
|---|---|---|
| Orchestrateur (session principale) | Opus 5.5, medium | Il cadre, découpe, tranche et lit les rendus. Peu de tokens, beaucoup de levier. Opus ne coûte que 2 fois Sonnet (4 $ contre 2 $ par million en entrée), et medium est l'effort par défaut recommandé pour Opus 5.5. |
| Codeur (`codeur`) | Sonnet 5.5, medium | C'est là que partent la majorité des tokens. La doc : « Sonnet handles most coding tasks well and costs less than Opus. » |
| Auditeur (`auditeur`) | Sonnet 5.5, high | La doc réserve `high` au « travail où la vérification compte ». Lecture seule, regard neuf sur le diff. |
| Relecteur externe (`relecteur`) | Opus 5.5, high | Revue d'architecture et de risques, par vague ou avant publication, pas par chantier, pour garder son coût rare. |
| Exploration (`Explore` surchargé) | Haiku 5.5 | Sur abonnement, l'Explore intégré tourne sur Opus. La doc permet de le surcharger par un agent du même nom : Haiku coûte 40 fois moins qu'Opus en entrée pour des recherches mécaniques. |
| Agents génériques | Sonnet (`CLAUDE_CODE_SUBAGENT_MODEL`) | Sans ça, un agent `general-purpose` hériterait d'Opus. |

Effort : `medium` partout par défaut. `ultrathink` ou `/effort xhigh` pour un tour difficile (architecture, physique), jamais en permanence : la doc prévient que `max` « peut sur-réfléchir » avec des gains décroissants.

**Alternative écartée pour l'instant** : orchestrateur Sonnet avec Opus en conseiller (`advisorModel: "opus"`), le couple que la doc d'Anthropic donne pour économiser. Ce serait le bon réglage si les limites d'usage deviennent le problème : il suffit de mettre `"model": "sonnet"` et `"advisorModel": "opus"` dans `.claude/settings.json`. Pour démarrer un projet où l'architecture se décide, on préfère Opus aux commandes.

## Le cycle d'un chantier
1. **Cadrer** (`/cadrer`) : interview courte, puis `specs/<id>.md` avec critères d'acceptation vérifiables et niveau de contrôle. Pratique recommandée par Anthropic (« let Claude interview you », spec autonome, nouvelle session pour l'exécuter).
2. **Déléguer** (`/deleguer`) : brief d'une page qui cite au lieu de recopier. Un codeur par zone de code ; en parallèle seulement sur des zones disjointes, chacun dans un worktree.
3. **Coder** : test d'abord, tests ciblés pendant le travail, PR vers `main`.
4. **Auditer** selon le niveau : *complet* (physique, état de partie, sauvegardes), *léger* (consultation, options), *aucun* (libellés, docs). La doc recommande une revue adverse à contexte frais, mais avertit qu'un relecteur « trouvera toujours quelque chose » : l'auditeur ne signale que ce qui touche la justesse ou les critères.
5. **Relire** (`relecteur`) à la fin d'une vague ou avant une publication. Les skills intégrés `/code-review` et `/security-review` restent disponibles pour une revue ponctuelle d'un diff.
6. **Passation** (`/passation`) : `docs/handoff.md` réécrit en place (40 lignes au plus) et `docs/chantiers.md` à jour, pour qu'une nouvelle session reprenne sans relire.
7. **Fusionner** : la session principale fusionne la PR dès qu'elle est prête (CI verte, audit sans bloquant, revue close). GitHub n'applique pas de ruleset sur un dépôt privé sans compte payant : la session vérifie elle-même les quatre jobs sur le dernier commit et passe `expectedHeadSha`, pour qu'un push arrivé entre-temps fasse échouer la fusion. Les sous-agents ne fusionnent pas, et une PR qui modifie le workflow attend Guillaume.

## Économie de tokens : ce qui est en place
- `CLAUDE.md` court (environ 50 lignes ; la doc vise moins de 200 et dit qu'un fichier trop long fait ignorer les règles). Les procédures vivent dans des skills, chargés seulement quand ils servent.
- Agents seulement quand ils paient : chacun redémarre à froid et ne partage pas le cache de la session. Seuil retenu : déléguer si le travail inline dépasserait environ 30 000 tokens ou doit être isolé (règle de la boîte à outils, appuyée par une mesure : un redémarrage de serveur de 4 commandes délégué a brûlé 67 000 tokens).
- `autoCompactWindow: 300000` : sur un modèle à 1 M de tokens, la compaction automatique n'arrive qu'à environ 967 000 tokens. Chaque requête relit tout le contexte ; mieux vaut compacter tôt, avec les consignes de compaction de `CLAUDE.md`.
- Ligne d'état : modèle, contexte en milliers de tokens, coût estimé, limite des 5 h. On voit le contexte grossir avant qu'il coûte.
- Formatage par hook (`post-edit-format.sh`) : zéro token, au lieu de demander à Claude de reformater.
- Reçu de démarrage de 3 lignes plutôt que le contenu du handoff : Claude lit le handoff seulement si la demande ne nomme pas déjà la tâche (voir plus bas).
- Habitudes (dans `CLAUDE.md`) : chercher avant de lire, filtrer les sorties, `/clear` entre deux sujets, `/clear` après deux corrections ratées, `/btw` pour une question jetable.

## Hooks
| Hook | Événement | Rôle |
|---|---|---|
| `garde-fou.sh` | PreToolUse Bash | Refuse : push sur `main`, push forcé (sauf `--force-with-lease`), `rm -r` sur `/`, `~`, `.`, `..` ou `*`, `pkill`/`killall`, lecture d'un `.env`. Échoue ouvert si `jq` manque. |
| `session-start.sh` | SessionStart | Reçu : branche, taille du handoff, nombre de chantiers ouverts. |
| `post-edit-format.sh` | PostToolUse Write/Edit | Formate le fichier si l'outil est installé (prettier, ruff, gdformat, rustfmt, dotnet format). Silencieux. |
| `statusline.sh` | ligne d'état | Voir plus haut. |

Testés à la main sur 25 commandes (refus et cas légitimes) avant la PR. Les `permissions.deny` bloquent aussi la lecture et l'écriture des `.env` par les outils de fichiers.

**À ajouter quand la stack existera** : un hook `Stop` qui lance les tests rapides et renvoie l'échec à Claude (code 2), avec une garde sur `stop_hook_active` pour éviter une boucle ; et le plugin d'intelligence de code (LSP) du langage choisi, que la doc recommande pour remplacer des lectures de fichiers par « aller à la définition ».

## Ce qui vient de claude-harness-toolbox
Le dépôt a été lu en entier avant tout choix (skills, hooks, docs, mesures). Sa propre règle est « voler le motif, ne pas installer la chose » ; c'est ce qu'on a fait.

**Retenu et adapté en français** : `prevent` → `prevenir`, `prove` → `prouver`, `redteam` → `contre-expertise`, `outside` → `voir-ailleurs`, `grill-me` et `spec` fusionnés dans `cadrer`, `checkpoint` simplifié en `passation`. Motifs retenus : le reçu plutôt que le contenu au démarrage, le garde-fou par hook plutôt que par consigne, le seuil de coût avant de déléguer, l'état réécrit en place avec un plafond de lignes.

**Écarté** :
- Le plugin entier : ses hooks câblés sont la version Windows PowerShell, et ses 14 skills ajoutent leurs descriptions à chaque session.
- La porte d'orientation (`orient_gate`), qui bloque la première écriture tant que le fichier d'état n'est pas lu. Leurs propres 154 essais mesurent que ce rituel *coûte* +35 % à +136 % de tokens de sortie quand la demande est déjà précise, et ne gagne que sur du travail exploratoire multi-projets. Golferoo est un seul projet piloté par des briefs précis.
- `fanout` (5 agents et un critique : cher), `necromancy` et `log` (scripts PowerShell), `new-project`, `spinoff`, `vet`, `plain`, `dumb`, `breakdown` (doublons du mode plan ou de `prevenir`), `grill_gate`, `guard` (pannes propres à Windows), `tmux_receipt`, le pont Codex.

## Ce qui vient de la communauté, et ce qui a été écarté
Retenu : formatage et lint par hook PostToolUse, hooks de sécurité en PreToolUse (un PostToolUse arrive trop tard), agents de recherche bornés à des outils en lecture, petits briefs, éviter les agents en parallèle sur des zones qui se chevauchent, mesurer avec `/usage` et `/context`.

Écarté : les équipes d'agents (expérimentales, environ 7 fois plus de tokens selon Anthropic), les gros « frameworks » de prompts et les listes de 20 agents spécialisés (contexte gonflé, descriptions chargées à chaque session), les hooks `UserPromptSubmit` qui ajoutent du contexte à chaque message, les hooks qui appellent un modèle (lents et payants).

## Auto-amélioration
Une boucle en trois temps, conçue pour coûter presque rien entre deux revues.

1. **Capturer en continu, pour une ligne.** Chaque rendu d'agent finit par `Friction : <une ligne ou aucune>`. L'orchestrateur ajoute les vraies frictions à `docs/lecons.md` par `echo >>`, sans relire le fichier ; `/prevenir` y journalise aussi chaque règle ignorée. Coût : quelques dizaines de tokens par chantier.
2. **Déclencher sans y penser.** `verif-budgets.sh` mesure la taille de `CLAUDE.md` (60 lignes max), du handoff (40), des descriptions de skills et d'agents (4 000 caractères, chargées à chaque session) et le nombre de leçons en attente (8). Le reçu de démarrage n'imprime une ligne que si un budget est dépassé : zéro token quand tout va bien. Une revue se lance aussi en fin de vague, après le relecteur.
3. **Réviser par lot, avec un humain.** `/ameliorer` regroupe les leçons par famille. Une leçon isolée attend (sauf si elle est grave) ; deux occurrences de la même famille déclenchent une correction dans la couche la plus étroite (hook, skill, agent, et `CLAUDE.md` en dernier). Chaque revue doit aussi élaguer, et vise un solde de lignes nul ou négatif dans `CLAUDE.md`. Tout passe par une PR que Guillaume fusionne : le workflow ne se modifie jamais seul.

La revue suivante vérifie si les leçons traitées sont revenues : si oui, la correction était dans la mauvaise couche. C'est ce qui ferme la boucle.

**Pourquoi pas plus automatique ?** Un hook `Stop` ou `UserPromptSubmit` qui ferait réfléchir Claude à chaque tour coûterait des tokens à chaque tour pour un gain rare, et la doc d'Anthropic met en garde contre un `CLAUDE.md` qui grossit d'une règle par incident (« Claude ignore la moitié des règles »). La règle des deux occurrences et l'élagage obligatoire empêchent cette dérive. La seule mesure objective du coût est `/usage` (part des skills, agents et MCP, avec recommandations) : on la colle dans la revue quand on l'a sous la main.

### Journal des revues
- 2026-10-24 · mise en place de la boucle (aucune leçon encore).

## Pour faire évoluer le workflow
- Une friction : une ligne dans `docs/lecons.md`. Une revue : `/ameliorer`.
- Une règle oubliée deux fois : `/prevenir` la déplace dans la couche qui sera sûrement lue (hook, skill, agent, `CLAUDE.md`).
- `/usage` montre la part des skills, agents et serveurs MCP dans la consommation, avec des recommandations.
- `/insights` produit un rapport sur les frictions récentes.

## Sources
- [Best practices for Claude Code](https://code.claude.com/docs/en/best-practices)
- [Manage costs effectively](https://code.claude.com/docs/en/costs)
- [Model configuration](https://code.claude.com/docs/en/model-config)
- [Create custom subagents](https://code.claude.com/docs/en/sub-agents)
- [Advisor tool](https://code.claude.com/docs/en/advisor)
- [Status line](https://code.claude.com/docs/en/statusline)
- [Tarifs de l'API](https://platform.claude.com/docs/en/about-claude/pricing)
- [claude-harness-toolbox](https://github.com/dtiger1889-ops/claude-harness-toolbox) et son [rapport de mesures](https://github.com/dtiger1889-ops/harness-benchmark-report)
- [Hooks playbook (Proxify)](https://agentic.proxify.io/setup/hooks-playbook)
- [24 Claude Code tips (dev.to)](https://dev.to/oikon/24-claude-code-tips-claudecodeadventcalendar-52b5)
- [Stop Claude Code subagents burning tokens (Aident)](https://aident.ai/blog/stop-claude-code-subagents-burning-tokens)
- [Claude Code subagents cost tokens (MindStudio)](https://www.mindstudio.ai/blog/claude-code-subagents-cost-tokens)
