#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# ResolveLite — Phase 1 · Sub-chunk 6.1 · Provision the Azure foundation
# Usage:  ./deploy/01-foundation.sh        (from the repo root)
# Safe to re-run: create commands are idempotent or skip existing items.
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail
source "$(dirname "$0")/00-variables.sh"

echo "==> 0. Pre-flight: who am I, and which subscription?"
az account show --query "{subscription:name, id:id, user:user.name}" -o table
az extension add --name containerapp --upgrade --only-show-errors
for ns in Microsoft.App Microsoft.ContainerRegistry Microsoft.OperationalInsights \
          Microsoft.Storage Microsoft.KeyVault Microsoft.ManagedIdentity Microsoft.Web; do
  echo "   registering $ns"
  az provider register --namespace "$ns" --wait --only-show-errors
done

echo "==> 1. Resource group: $RG"
az group create -n "$RG" -l "$LOCATION" --tags $TAGS -o none

echo "==> 2. Log Analytics workspace: $LOG"
az monitor log-analytics workspace create -g "$RG" -n "$LOG" -l "$LOCATION" \
  --retention-time 30 --tags $TAGS -o none

echo "==> 3. User-assigned managed identity: $UAMI"
az identity create -g "$RG" -n "$UAMI" -l "$LOCATION" --tags $TAGS -o none
UAMI_ID=$(az identity show -g "$RG" -n "$UAMI" --query id -o tsv)
UAMI_PRINCIPAL_ID=$(az identity show -g "$RG" -n "$UAMI" --query principalId -o tsv)
UAMI_CLIENT_ID=$(az identity show -g "$RG" -n "$UAMI" --query clientId -o tsv)

echo "==> 4. Storage account (Entra-only, no anonymous blobs): $ST"
az storage account create -g "$RG" -n "$ST" -l "$LOCATION" \
  --sku Standard_LRS --kind StorageV2 --access-tier Hot \
  --https-only true --min-tls-version TLS1_2 \
  --allow-blob-public-access false \
  --allow-shared-key-access false \
  --tags $TAGS -o none
ST_ID=$(az storage account show -g "$RG" -n "$ST" --query id -o tsv)
# Control-plane create: works immediately, no data-plane role needed yet.
az storage container-rm create --storage-account "$ST" -g "$RG" \
  -n "$BLOB_CONTAINER" --public-access off -o none

echo "==> 5. Container registry (Basic, admin user OFF): $ACR"
az acr create -g "$RG" -n "$ACR" -l "$LOCATION" \
  --sku Basic --admin-enabled false --tags $TAGS -o none
ACR_ID=$(az acr show -n "$ACR" --query id -o tsv)
echo "   ARM-audience token auth (needed for App Service managed-identity pulls):"
az acr config authentication-as-arm show -r "$ACR" -o table || true

echo "==> 6. Key Vault (RBAC permission model, 7-day soft delete for dev): $KV"
az keyvault create -g "$RG" -n "$KV" -l "$LOCATION" \
  --enable-rbac-authorization true --retention-days 7 --tags $TAGS -o none
KV_ID=$(az keyvault show -n "$KV" --query id -o tsv)

echo "==> 7. Role assignments (least privilege, narrowest scope)"
ME_ID=$(az ad signed-in-user show --query id -o tsv)
assign() { # <principalObjectId> <User|ServicePrincipal> <role> <scope>
  az role assignment create --assignee-object-id "$1" --assignee-principal-type "$2" \
    --role "$3" --scope "$4" -o none \
  && echo "   ✔ $3 → $(basename "$4") for $2" \
  || echo "   ↳ skipped (may already exist): $3 on $(basename "$4")"
}
# The app's identity
assign "$UAMI_PRINCIPAL_ID" ServicePrincipal "AcrPull"                       "$ACR_ID"
assign "$UAMI_PRINCIPAL_ID" ServicePrincipal "Storage Blob Data Contributor" "$ST_ID"
assign "$UAMI_PRINCIPAL_ID" ServicePrincipal "Key Vault Secrets User"        "$KV_ID"
# You (local development + labs)
assign "$ME_ID" User "Storage Blob Data Contributor" "$ST_ID"
assign "$ME_ID" User "Key Vault Secrets Officer"     "$KV_ID"
assign "$ME_ID" User "AcrPush"                       "$ACR_ID"

echo "==> 8. Seed a demo secret for the secret-handling labs (6.5 / 6.6)"
retry az keyvault secret set --vault-name "$KV" -n external-api-key \
  --value "demo-not-a-real-key-$RANDOM" -o none

echo "==> 9. Container Apps environment (Consumption, logs → Log Analytics): $CAE"
LOG_CUSTOMER_ID=$(az monitor log-analytics workspace show -g "$RG" -n "$LOG" --query customerId -o tsv)
LOG_KEY=$(az monitor log-analytics workspace get-shared-keys -g "$RG" -n "$LOG" --query primarySharedKey -o tsv)
az containerapp env create -g "$RG" -n "$CAE" -l "$LOCATION" \
  --logs-workspace-id "$LOG_CUSTOMER_ID" --logs-workspace-key "$LOG_KEY" \
  --tags $TAGS -o none

echo "==> 10. Save non-secret outputs for later sub-chunks"
cat > "$(dirname "$0")/.outputs.env" <<EOF
UAMI_ID=$UAMI_ID
UAMI_CLIENT_ID=$UAMI_CLIENT_ID
UAMI_PRINCIPAL_ID=$UAMI_PRINCIPAL_ID
ST_ID=$ST_ID
ACR_ID=$ACR_ID
ACR_LOGIN_SERVER=$(az acr show -n "$ACR" --query loginServer -o tsv)
KV_ID=$KV_ID
KV_URI=$(az keyvault show -n "$KV" --query properties.vaultUri -o tsv)
BLOB_ENDPOINT=$(az storage account show -g "$RG" -n "$ST" --query primaryEndpoints.blob -o tsv)
EOF

echo ""
echo "✅ Foundation ready. Non-secret IDs saved to deploy/.outputs.env"
echo "   Next: ./deploy/02-verify.sh"
