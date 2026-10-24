#!/usr/bin/env bash
# SessionStart : un reçu de quelques lignes, jamais le contenu des fichiers.
# Claude lit docs/handoff.md lui-même s'il en a besoin (la demande ne nomme pas déjà la tâche).
cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0
branche="$(git branch --show-current 2>/dev/null)"
echo "Golferoo · branche ${branche:-?}"
if [ -f docs/handoff.md ]; then
  quand="$(git log -1 --format=%cr -- docs/handoff.md 2>/dev/null)"
  echo "docs/handoff.md : $(wc -l < docs/handoff.md) lignes, commité ${quand:-jamais}. À lire avant de reprendre un chantier ; inutile si la demande nomme déjà la tâche."
fi
if [ -f docs/chantiers.md ]; then
  ouverts="$(grep -Ec '^\|[^|]*\|[^|]*\|[[:space:]]*(en cours|à faire|bloqué)' docs/chantiers.md 2>/dev/null)"
  echo "docs/chantiers.md : ${ouverts:-0} chantier(s) ouvert(s)."
fi
exit 0
