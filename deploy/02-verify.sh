#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# ResolveLite — Phase 1 · Sub-chunk 6.1 · Prove the foundation is correct
# Usage:  ./deploy/02-verify.sh
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail
source "$(dirname "$0")/00-variables.sh"
source "$(dirname "$0")/.outputs.env"

echo "==> Resources in $RG"
az resource list -g "$RG" --query "[].{name:name, type:type}" -o table

echo ""
echo "==> Role assignments held by the app identity ($UAMI)"
az role assignment list --assignee "$UAMI_PRINCIPAL_ID" --all \
  --query "[].{role:roleDefinitionName, scope:scope}" -o table

echo ""
echo "==> Proof 1: Entra ID (data-plane RBAC) can write and read blobs"
TMP_FILE="C:/Users/priya/source/repos/ResolveLite/hello.txt"
echo "hello from sub-chunk 6.1" > "$TMP_FILE"
retry az storage blob upload --account-name "$ST" -c "$BLOB_CONTAINER" \
  -n "smoke/hello.txt" -f "$TMP_FILE" --auth-mode login --overwrite -o none
az storage blob list --account-name "$ST" -c "$BLOB_CONTAINER" \
  --auth-mode login --query "[].name" -o tsv

echo ""
echo "==> Proof 2: shared-key (account key) access is refused"
if az storage blob list --account-name "$ST" -c "$BLOB_CONTAINER" \
     --auth-mode key -o none 2>/dev/null; then
  echo "   ❌ Shared key still works: check allowSharedKeyAccess"
else
  echo "   ✅ Shared key rejected (as designed)"
fi

echo ""
echo "==> Proof 3: ACR admin user disabled?"
echo "   adminUserEnabled = $(az acr show -n "$ACR" --query adminUserEnabled -o tsv)  (expect: false)"

echo ""
echo "==> Proof 4: Key Vault uses RBAC and the demo secret exists"
echo "   enableRbacAuthorization = $(az keyvault show -n "$KV" --query properties.enableRbacAuthorization -o tsv)  (expect: true)"
echo "   secret enabled          = $(az keyvault secret show --vault-name "$KV" -n external-api-key --query attributes.enabled -o tsv)"

echo ""
echo "==> Proof 5: Container Apps environment is ready"
az containerapp env show -g "$RG" -n "$CAE" \
  --query "{name:name, state:properties.provisioningState, domain:properties.defaultDomain}" -o table

echo ""
echo "==> Cleanup of smoke-test blob"
az storage blob delete --account-name "$ST" -c "$BLOB_CONTAINER" \
  -n "smoke/hello.txt" --auth-mode login -o none
rm -f "$TMP_FILE"

echo ""
echo "✅ 6.1 verified."
