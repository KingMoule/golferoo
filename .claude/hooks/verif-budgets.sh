#!/usr/bin/env bash
# Vérifie les budgets de taille du workflow. Sans argument : rapport complet.
# --alerte : n'imprime que les dépassements (pour le reçu de démarrage, zéro token si tout va bien).
cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0
mode="${1:-}"
alertes=()
ok=()

verifier() { # libellé valeur max
  if [ "$2" -gt "$3" ]; then alertes+=("$1 : $2 (max $3)"); else ok+=("$1 : $2 (max $3)"); fi
}

[ -f CLAUDE.md ] && verifier "CLAUDE.md, lignes" "$(wc -l < CLAUDE.md)" 60
[ -f docs/handoff.md ] && verifier "docs/handoff.md, lignes" "$(wc -l < docs/handoff.md)" 40

desc_total=0
for f in .claude/skills/*/SKILL.md .claude/agents/*.md; do
  [ -f "$f" ] || continue
  n="$(awk '/^description:/{sub(/^description:[[:space:]]*/,""); print length; exit}' "$f")"
  desc_total=$((desc_total + ${n:-0}))
done
verifier "descriptions des skills et agents, caractères" "$desc_total" 4000

if [ -f docs/lecons.md ]; then
  verifier "leçons en attente" "$(grep -c '^- [0-9]' docs/lecons.md)" 8
fi

if [ "$mode" = "--alerte" ]; then
  [ ${#alertes[@]} -gt 0 ] && printf 'Budget dépassé : %s. Lance /ameliorer quand la tâche en cours le permet.\n' "$(IFS='; '; echo "${alertes[*]}")"
  exit 0
fi
for l in "${ok[@]}"; do echo "ok      $l"; done
for l in "${alertes[@]}"; do echo "DÉPASSÉ $l"; done
exit 0
