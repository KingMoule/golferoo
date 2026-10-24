#!/usr/bin/env bash
# PreToolUse (Bash) : refuse une poignée d'opérations irréversibles.
# Exit 2 = refus, le message sur stderr est renvoyé à Claude. Tout le reste passe.
# Échoue ouvert : sans jq ou sans commande lisible, on laisse passer.
command -v jq >/dev/null 2>&1 || exit 0
cmd="$(jq -r '.tool_input.command // empty' 2>/dev/null)"
[ -z "$cmd" ] && exit 0

refuse() { echo "GARDE-FOU : $1" >&2; exit 2; }

# Push : jamais sur main/master, jamais forcé (--force-with-lease reste permis).
if printf '%s' "$cmd" | grep -Eq '(^|[;&|[:space:]])git[[:space:]]+push([[:space:]]|$)'; then
  printf '%s' "$cmd" | grep -Eq -- '(^|[[:space:]])(-f|--force)([[:space:]]|$)' \
    && refuse "push forcé refusé. Utilise --force-with-lease sur ta propre branche si c'est vraiment nécessaire."
  printf '%s' "$cmd" | grep -Eq '([[:space:]:+]|^)(main|master)([[:space:]]|$)' \
    && refuse "push vers main refusé. Pousse ta branche de chantier et ouvre une PR vers main."
fi

# Fusion : seul Guillaume fusionne.
printf '%s' "$cmd" | grep -Eq '(^|[;&|[:space:]])gh[[:space:]]+pr[[:space:]]+merge' \
  && refuse "un agent ne fusionne jamais une PR. Signale-la comme prête."

# Suppression récursive de la racine, du home, du parent ou d'un joker nu.
printf '%s' "$cmd" | grep -Eq -- 'rm[[:space:]]+(-[-a-zA-Z]+[[:space:]]+)+(/|~|\$HOME|\.\.?|\*)/?\*?([[:space:];&|]|$)' \
  && refuse "suppression récursive trop large. Vise un chemin précis."

# Tuer par motif : on tue par PID.
printf '%s' "$cmd" | grep -Eq '(^|[;&|[:space:]])(pkill|killall)([[:space:]]|$)' \
  && refuse "pkill/killall refusés : ils peuvent tuer la session elle-même. Trouve le PID et utilise kill <PID>."

# Secrets.
printf '%s' "$cmd" | grep -Eq '(cat|less|head|tail|more|bat)[[:space:]]+[^|;&]*\.env([.[:space:]]|$)' \
  && refuse "lecture d'un fichier .env refusée. Demande la valeur à Guillaume si elle est nécessaire."

exit 0
