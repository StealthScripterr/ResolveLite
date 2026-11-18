# Sub-chunk 6.4 — Cloud Shell commands (copy-paste reference)

Open the Azure Portal → click the **Cloud Shell** icon (`>_`) in the top bar → **Bash**.
Paste each block, one at a time. Replace `<your-github-user>` first.

## 1. Variables

```bash
# Your registry and your public GitHub repository
ACR=crresolvelitedevcin001
REPO=https://github.com/<your-github-user>/resolvelite.git

# Latest commit on main, shortened to 7 characters (e.g. 3f2a9c1)
SHA=$(git ls-remote "$REPO" refs/heads/main | cut -c1-7)
echo "Building commit: $SHA"
```

## 2. Build, smoke-test and push (multi-step ACR task)

```bash
az acr run \
  --registry "$ACR" \
  --file acr-task.yaml \
  --set sha="$SHA" \
  "$REPO#main"
```

## 3. (Alternative) Quick task: build and push only, no smoke test

```bash
az acr build \
  --registry "$ACR" \
  --image "resolvelite/app:sha-$SHA" \
  --build-arg APP_VERSION="sha-$SHA" \
  --file Dockerfile \
  "$REPO#main"
```

## 4. Give the tested image a release tag (no rebuild, no download)

```bash
az acr import \
  --name "$ACR" \
  --source "$ACR.azurecr.io/resolvelite/app:sha-$SHA" \
  --image "resolvelite/app:1.0.0"
```

## 5. Lock the release tag (cannot be overwritten or deleted)

```bash
az acr repository update \
  --name "$ACR" \
  --image "resolvelite/app:1.0.0" \
  --write-enabled false \
  --delete-enabled false

# Confirm the lock
az acr repository show --name "$ACR" --image "resolvelite/app:1.0.0" \
  --query changeableAttributes -o table
```

## 6. (Optional exam lab) Automatic rebuild when the .NET base image is patched

```bash
az acr task create \
  --registry "$ACR" \
  --name resolvelite-app-patch \
  --context "$REPO#main" \
  --file Dockerfile \
  --image "resolvelite/app:patch-{{.Run.ID}}" \
  --arg APP_VERSION=patch \
  --commit-trigger-enabled false \
  --base-image-trigger-enabled true \
  --base-image-trigger-type Runtime

# Run once so ACR records which base image the app depends on
az acr task run --registry "$ACR" --name resolvelite-app-patch
```
