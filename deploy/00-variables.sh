#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# ResolveLite — shared variables & helpers (Phase 1)
# Usage:  source deploy/00-variables.sh
# Every other script in deploy/ sources this file, so names live in ONE place.
# ─────────────────────────────────────────────────────────────────────────────

# Git Bash on Windows rewrites "/subscriptions/..." into a Windows path. Stop that.
export MSYS_NO_PATHCONV=1

# ── Naming parts (CAF pattern: {type}-{workload}-{env}-{region}-{instance}) ──
export APP="resolvelite"
export ENV="dev"
export LOCATION="koreacentral"
export LOC="kc"
export INSTANCE="001"

# Storage, ACR and Key Vault names are GLOBALLY unique.
# If creation fails with "name already taken", re-run with a 2-character suffix:
#   SUFFIX=x7 ./deploy/01-foundation.sh
export SUFFIX="${SUFFIX:-}"

# ── Resource names ───────────────────────────────────────────────────────────
export RG="rg-${APP}-${ENV}-${LOC}-${INSTANCE}"
export LOG="log-${APP}-${ENV}-${LOC}-${INSTANCE}"
export UAMI="id-${APP}-app-${ENV}-${LOC}-${INSTANCE}"
export ST="st${APP}${ENV}${LOC}${INSTANCE}${SUFFIX}"     # 3–24, lowercase + digits
export ACR="cr${APP}${ENV}${LOC}${INSTANCE}${SUFFIX}"    # 5–50, alphanumeric
if [[ -z "$SUFFIX" ]]; then                              # 3–24 chars max
  export KV="kv-${APP}-${ENV}-${LOC}01"
else
  export KV="kv-${APP}-${ENV}-${LOC}${SUFFIX}"
fi
export CAE="cae-${APP}-${ENV}-${LOC}-${INSTANCE}"
export CA="ca-${APP}-app-${ENV}"
export BLOB_CONTAINER="knowledge"

# ── Mandatory tags (word-split on purpose when used as: --tags $TAGS) ───────
OWNER="$(az account show --query user.name -o tsv 2>/dev/null || echo unknown)"
export TAGS="app=${APP} env=${ENV} owner=${OWNER} phase=1 costCenter=learning"

# ── Helper: retry a command while new role assignments propagate ────────────
retry() {
  local n=0
  until "$@"; do
    n=$((n + 1))
    if [[ $n -ge 10 ]]; then
      echo "   ✖ still failing after $n attempts: $*" >&2
      return 1
    fi
    echo "   …waiting for RBAC propagation ($n/10), retrying in 15s"
    sleep 15
  done
}
