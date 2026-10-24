#!/usr/bin/env bash
# Ligne d'état : modèle, contexte en milliers de tokens, coût estimé, limite des 5 h.
# En contexte 1M, un pourcentage reste petit longtemps : les tokens absolus parlent mieux.
command -v jq >/dev/null 2>&1 || { echo "golferoo"; exit 0; }
jq -r '
  def k: if . == null then "?" else ((. / 1000) | floor | tostring) + "k" end;
  [ (.model.display_name // "?"),
    ("ctx " + ((.context_window.total_input_tokens) | k)),
    ("$" + ((.cost.total_cost_usd // 0) * 100 | round / 100 | tostring)),
    (if .rate_limits.five_hour.used_percentage then "5h " + (.rate_limits.five_hour.used_percentage | floor | tostring) + "%" else empty end)
  ] | join(" · ")' 2>/dev/null || echo "golferoo"
