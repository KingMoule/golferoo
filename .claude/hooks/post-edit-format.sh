#!/usr/bin/env bash
# PostToolUse (Write|Edit) : formate le fichier touché si l'outil du langage est installé.
# Silencieux et toujours exit 0 : le formatage ne coûte aucun token et ne bloque jamais.
command -v jq >/dev/null 2>&1 || exit 0
f="$(jq -r '.tool_input.file_path // empty' 2>/dev/null)"
[ -n "$f" ] && [ -f "$f" ] || exit 0
cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0

case "$f" in
  *.js|*.jsx|*.ts|*.tsx|*.mjs|*.cjs|*.css|*.scss|*.html|*.json|*.vue|*.svelte)
    [ -x node_modules/.bin/prettier ] && node_modules/.bin/prettier --write --log-level silent "$f" >/dev/null 2>&1 ;;
  *.py)
    command -v ruff >/dev/null 2>&1 && ruff format --quiet "$f" >/dev/null 2>&1 ;;
  *.gd)
    command -v gdformat >/dev/null 2>&1 && gdformat "$f" >/dev/null 2>&1 ;;
  *.rs)
    command -v rustfmt >/dev/null 2>&1 && rustfmt --edition 2021 "$f" >/dev/null 2>&1 ;;
  *.cs)
    command -v dotnet >/dev/null 2>&1 && dotnet format whitespace --include "$f" >/dev/null 2>&1 ;;
esac
exit 0
