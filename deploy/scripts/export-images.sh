#!/usr/bin/env bash
# =============================================================================
# tpm-pregledi — build + export docker image-a u .tar (Elixir blueprint 11 §3, 17 §5).
# =============================================================================
# Na mašini sa Docker-om, iz korena repoa:
#   bash deploy/scripts/export-images.sh              # oba image-a
#   bash deploy/scripts/export-images.sh --frontend   # samo frontend (6× manji prenos)
# Rezultat: tpm-pregledi-images-<verzija>-<sha>.tar ili tpm-pregledi-frontend-<verzija>-<sha>.tar
# (gitignored; obriši posle prenosa).
#
# Kontekst build-a je `git archive HEAD` (ne disk): image sadrži TAČNO commit-ovano
# stanje i zaobilazi BuildKit "invalid file request" bag na Windows/NTFS.
# =============================================================================
set -euo pipefail

SLUG="tpm-pregledi"
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_ROOT="$( cd "${SCRIPT_DIR}/../.." && pwd )"
SAMO_FRONTEND=0; [[ "${1:-}" == "--frontend" ]] && SAMO_FRONTEND=1

if [[ -t 1 ]]; then C_GREEN=$'\033[0;32m'; C_RED=$'\033[0;31m'; C_YELLOW=$'\033[1;33m'; C_RESET=$'\033[0m'
else C_GREEN=''; C_RED=''; C_YELLOW=''; C_RESET=''; fi
log()  { echo "${C_GREEN}==>${C_RESET} $*"; }
warn() { echo "${C_YELLOW}==>${C_RESET} $*"; }
die()  { echo "${C_RED}!! $*${C_RESET}" >&2; exit 1; }

command -v docker >/dev/null 2>&1 || die "Docker nije dostupan. Build ide na mašini sa Docker-om (Docker Desktop / WSL)."
docker info >/dev/null 2>&1       || die "Docker daemon ne radi. Pokreni Docker Desktop."
cd "${REPO_ROOT}"

# ─── verzija: VERSION + git SHA ───
[[ -f VERSION ]] || die "Nema VERSION fajla u korenu repoa."
VERSION="$(tr -d '[:space:]' < VERSION)"
[[ "${VERSION}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "VERSION '${VERSION}' nije MAJOR.MINOR.PATCH."
SHA="$(git rev-parse --short=7 HEAD)"
IMAGE_TAG="${VERSION}-${SHA}"
BUILD_UTC="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

[[ -z "$(git status --porcelain)" ]] || warn "Radni direktorijum nije čist — NECOMMITOVANE izmene NEĆE ući u image."
TAGOVI="$(git tag --points-at HEAD)"
if ! grep -qxF "v${VERSION}" <<< "${TAGOVI}"; then
    warn "HEAD nije tagovan v${VERSION} — ovo NIJE release build (release: chore(release) commit + git tag v${VERSION})."
fi

if (( SAMO_FRONTEND )); then
    TAR_FILE="${REPO_ROOT}/${SLUG}-frontend-${IMAGE_TAG}.tar"
else
    TAR_FILE="${REPO_ROOT}/${SLUG}-images-${IMAGE_TAG}.tar"
    log "Build backend ${SLUG}/backend:${IMAGE_TAG}…"
    git archive "HEAD:backend" | DOCKER_BUILDKIT=1 docker build --pull \
        --build-arg APP_VERSION="${VERSION}" --build-arg GIT_SHA="${SHA}" --build-arg BUILD_UTC="${BUILD_UTC}" \
        -t "${SLUG}/backend:${IMAGE_TAG}" -
fi

log "Build frontend ${SLUG}/frontend:${IMAGE_TAG}…"
git archive "HEAD:frontend" | DOCKER_BUILDKIT=1 docker build --pull \
    --build-arg APP_VERSION="${VERSION}" --build-arg GIT_SHA="${SHA}" --build-arg BUILD_UTC="${BUILD_UTC}" \
    -t "${SLUG}/frontend:${IMAGE_TAG}" -

log "Export u ${TAR_FILE}…"
if (( SAMO_FRONTEND )); then
    docker save -o "${TAR_FILE}" "${SLUG}/frontend:${IMAGE_TAG}"
else
    docker save -o "${TAR_FILE}" "${SLUG}/backend:${IMAGE_TAG}" "${SLUG}/frontend:${IMAGE_TAG}"
fi
log "Gotovo: $(du -h "${TAR_FILE}" | cut -f1) — $(basename "${TAR_FILE}")"

cat <<EOF

Sledeći koraci:
  1. (ako verzija donosi migraciju) na hostu PRVO: deploy/scripts/backup.sh
  2. VPN → RDP Jump → MobaXterm SFTP → prenesi tar u /opt/apps/${SLUG}/
  3. Na hostu:  cd /opt/apps/${SLUG} && deploy/scripts/deploy.sh $(basename "${TAR_FILE}")
  4. Proveri „O aplikaciji" = ${VERSION}; obriši tar na obe strane; upiši u TODO.md.
EOF
