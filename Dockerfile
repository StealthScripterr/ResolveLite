# syntax=docker/dockerfile:1
# ─────────────────────────────────────────────────────────────────────────────
# ResolveLite: ONE image that contains the ASP.NET Core API + the compiled
# Angular UI. Three stages: "build big, ship small" (Chunk 3).
#
#   Stage 1  web    → Node compiles Angular into src/ResolveLite.Api/wwwroot
#   Stage 2  build  → .NET SDK restores, builds and publishes the API
#   Stage 3  final  → tiny, non-root runtime image with only the published app
#
# Build context = repository root (the folder that contains this file).
# ─────────────────────────────────────────────────────────────────────────────


# ═══ Stage 1: compile the Angular UI ═════════════════════════════════════════
# A glibc-based Node image (not Alpine) avoids native-binary mismatches in
# Angular's build tools. This stage never ships, so its size does not matter.
FROM node:22-slim AS web
WORKDIR /src/src/ResolveLite.Web

# Copy ONLY the dependency manifests first. Docker caches the `npm ci` layer
# until package.json or package-lock.json change, so code edits stay fast.
COPY src/ResolveLite.Web/package.json src/ResolveLite.Web/package-lock.json ./
RUN npm ci

# Now copy the Angular source and build it.
# angular.json sends the output to ../ResolveLite.Api/wwwroot
COPY src/ResolveLite.Web/ ./
RUN npx ng build --configuration production


# ═══ Stage 2: restore, build and publish the .NET API ═══════════════════════
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build

# The version shown by GET /api/info (e.g. "sha-3f2a9c1").
# ACR Tasks passes it in with --build-arg; local builds get the default.
ARG APP_VERSION=0.0.0-local

WORKDIR /src

# 2a) Restore using ONLY the project and props files. This layer stays cached
#     until a NuGet package changes, even when C# code changes every minute.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ResolveLite.Core/ResolveLite.Core.csproj                     src/ResolveLite.Core/
COPY src/ResolveLite.Infrastructure/ResolveLite.Infrastructure.csproj src/ResolveLite.Infrastructure/
COPY src/ResolveLite.Api/ResolveLite.Api.csproj                       src/ResolveLite.Api/
RUN dotnet restore src/ResolveLite.Api/ResolveLite.Api.csproj

# 2b) Copy the C# source code.
COPY src/ResolveLite.Core/           src/ResolveLite.Core/
COPY src/ResolveLite.Infrastructure/ src/ResolveLite.Infrastructure/
COPY src/ResolveLite.Api/            src/ResolveLite.Api/

# 2c) Bring in the compiled UI BEFORE publishing, so the static-assets
#     manifest used by MapStaticAssets() includes every Angular file.
COPY --from=web /src/src/ResolveLite.Api/wwwroot/ src/ResolveLite.Api/wwwroot/

# 2d) Publish a framework-dependent app (the runtime comes from Stage 3).
RUN dotnet publish src/ResolveLite.Api/ResolveLite.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /app/publish \
      -p:UseAppHost=false \
      -p:InformationalVersion=${APP_VERSION}


# ═══ Stage 3: the image that actually runs ══════════════════════════════════
# Ubuntu Chiseled: no shell, no package manager, runs as the non-root
# 'app' user (UID 1654) and listens on port 8080 by default.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish .

# Documentation only: the platform (Container Apps target port / App Service
# WEBSITES_PORT) decides which port receives traffic.
EXPOSE 8080

ENTRYPOINT ["dotnet", "ResolveLite.Api.dll"]
