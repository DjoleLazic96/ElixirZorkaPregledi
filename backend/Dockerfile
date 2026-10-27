# =============================================================================
# TPM pregledi — backend (ASP.NET Core 10) production image (Elixir blueprint 17 §4).
# Build kontekst: backend/  (restore sva 4 src projekta, NuGet cache layer).
# Build:  docker build -t tpm-pregledi/backend:<tag> --build-arg APP_VERSION=1.0.0 --build-arg GIT_SHA=abc1234 ./backend
# =============================================================================

# ─── 1) build ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore u zasebnom layer-u: samo csproj + props → cache se ne invalidira pri izmeni koda.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/TpmPregledi.Api/TpmPregledi.Api.csproj                       src/TpmPregledi.Api/
COPY src/TpmPregledi.Application/TpmPregledi.Application.csproj       src/TpmPregledi.Application/
COPY src/TpmPregledi.Domain/TpmPregledi.Domain.csproj                 src/TpmPregledi.Domain/
COPY src/TpmPregledi.Infrastructure/TpmPregledi.Infrastructure.csproj src/TpmPregledi.Infrastructure/
RUN dotnet restore src/TpmPregledi.Api/TpmPregledi.Api.csproj

# Verzija dolazi spolja (VERSION fajl nije u ovom kontekstu) — vidi Directory.Build.props.
# ARG tek ovde, posle restore-a, da promena SHA ne ruši NuGet cache layer.
ARG APP_VERSION=0.0.0-dev
ARG GIT_SHA=dev
ARG BUILD_UTC=

# tests/ se NE kopira u image.
COPY src/ src/
# EnableSourceControlManagerQueries=false: nema .git u kontekstu, pa SourceLink ne pokušava
# da pročita repo (inače warning); SourceRevisionId stiže eksplicitno.
RUN dotnet publish src/TpmPregledi.Api/TpmPregledi.Api.csproj \
        --configuration Release --no-restore --output /app/publish \
        -p:Version=${APP_VERSION} -p:SourceRevisionId=${GIT_SHA} -p:BuildUtc=${BUILD_UTC} \
        -p:EnableSourceControlManagerQueries=false

# ─── 2) runtime ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# curl samo za HEALTHCHECK. Ako aplikacija crta PDF (QuestPDF/SkiaSharp), dodati:
#   fontconfig fonts-dejavu-core   (potrebni za Š Ć Č Ž Đ u PDF-u)
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

# LABEL posle apt sloja — ARG vrednosti se menjaju svaki build i ne smeju da ruše apt cache.
ARG APP_VERSION=0.0.0-dev
ARG GIT_SHA=dev
ARG BUILD_UTC=
LABEL org.opencontainers.image.title="tpm-pregledi-backend" \
      org.opencontainers.image.version="${APP_VERSION}" \
      org.opencontainers.image.revision="${GIT_SHA}" \
      org.opencontainers.image.created="${BUILD_UTC}"

# Non-root: `app` (uid 1654) već postoji u MS aspnet image-u — NE praviti svog.
WORKDIR /app
COPY --from=build --chown=app:app /app/publish ./
# WORKDIR je root-owned 755 → bez chown-a `app` ne može da pravi podfoldere (Data Protection puca).
RUN chown app:app /app \
 && mkdir -p /app/logs /app/data-protection-keys /app/backups \
 && chown -R app:app /app/logs /app/data-protection-keys /app/backups
VOLUME ["/app/logs", "/app/data-protection-keys"]

USER app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=false

EXPOSE 8080

# /health/ready proverava i bazu — /live bi bio „zdrav" i bez baze.
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=4 \
    CMD curl --fail --silent http://127.0.0.1:8080/api/v1/health/ready || exit 1

ENTRYPOINT ["dotnet", "TpmPregledi.Api.dll"]
