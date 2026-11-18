#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# ResolveLite — delete EVERYTHING from Phase 1 (stops all billing).
# Run only when you are finished with the phase, or want a clean rebuild.
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail
source "$(dirname "$0")/00-variables.sh"

read -r -p "Delete resource group '$RG' and purge Key Vault '$KV'? Type DELETE to confirm: " ANSWER
[[ "$ANSWER" == "DELETE" ]] || { echo "Aborted."; exit 1; }

az group delete -n "$RG" --yes
# Key Vault is soft-deleted, so its name stays reserved until purged.
az keyvault purge -n "$KV" -l "$LOCATION" || echo "   (nothing to purge)"
rm -f "$(dirname "$0")/.outputs.env"
echo "✅ Torn down."
