#!/usr/bin/env bash
# =============================================================================
# tpm-pregledi — produkcioni deploy / update (Elixir blueprint 17 §6).
# =============================================================================
# Na docker hostu, iz /opt/apps/tpm-pregledi:
#   deploy/scripts/deploy.sh tpm-pregledi-images-<verzija>-<sha>.tar
#       → validacija .env → docker load → IMAGE_TAG iz tar-a → up -d → healthy → smoke → verzija
#   deploy/scripts/deploy.sh tpm-pregledi-frontend-<verzija>-<sha>.tar
#       → isto, backend se pretaguje iz trenutno deploy-ovane verzije (samo frontend izmenjen)
#
# Test samo validacije tajni (ne dira docker):
#   DEPLOY_VALIDATE_ONLY=1 deploy/scripts/deploy.sh
#
# Exit 0 = sve prošlo. Bilo šta drugo = aplikacija je verovatno na staroj verziji.
# =============================================================================
set -euo pipefail

SLUG="tpm-pregledi"
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_ROOT="$( cd "${SCRIPT_DIR}/../.." && pwd )"
COMPOSE_FILE="${REPO_ROOT}/docker-compose.yml"
ENV_FILE="${DEPLOY_ENV_FILE:-${REPO_ROOT}/.env}"

if [[ -t 1 ]]; then C_RED=$'\033[0;31m'; C_GREEN=$'\033[0;32m'; C_YELLOW=$'\033[1;33m'; C_RESET=$'\033[0m'
else C_RED=''; C_GREEN=''; C_YELLOW=''; C_RESET=''; fi
log()  { echo "${C_GREEN}==>${C_RESET} $*"; }
warn() { echo "${C_YELLOW}==>${C_RESET} $*"; }
die()  { echo "${C_RED}!! $*${C_RESET}" >&2; exit 1; }

compose() { docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" "$@"; }

# ─── .env se čita BEZ `source` ───────────────────────────────────────────────
# `source` bi izvršio fajl kao bash: lozinka sa ! ( ) ; & | se lomi ili iseče.
# Ovde: KEY=VALUE po liniji, navodnici (jednostruki ili dvostruki) se skidaju,
# vrednost je doslovna. Isto čita i compose.
ucitaj_env() {
    local line key val
    while IFS= read -r line || [[ -n "${line}" ]]; do
        [[ "${line}" =~ ^[[:space:]]*$ || "${line}" =~ ^[[:space:]]*# ]] && continue
        key="${line%%=*}"; val="${line#*=}"
        key="${key//[[:space:]]/}"
        [[ "${key}" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] || die "Nevalidna linija u ${ENV_FILE}: ${line}"
        if   [[ "${val}" =~ ^\'(.*)\'$ ]]; then val="${BASH_REMATCH[1]}"
        elif [[ "${val}" =~ ^\"(.*)\"$ ]]; then val="${BASH_REMATCH[1]}"
        fi
        printf -v "${key}" '%s' "${val}"; export "${key}"
    done < "${ENV_FILE}"
}

# ─── 1) Validacija tajni — jedina kapija između „imam .env" i „smem da deploy-ujem" ───
validate_secrets() {
    [[ -f "${ENV_FILE}" ]] || die "Ne postoji ${ENV_FILE}. Kopiraj .env.example u .env i popuni."
    grep -qE '<<.*>>' "${ENV_FILE}" && die "${ENV_FILE} sadrži <<...>> placeholder-e."
    ucitaj_env

    : "${APP_DOMAIN:?APP_DOMAIN nije postavljen}"
    : "${JWT_SECRET:?JWT_SECRET nije postavljen}"
    : "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD nije postavljen}"
    : "${ADMIN_INITIAL_PASSWORD:?ADMIN_INITIAL_PASSWORD nije postavljen}"

    [[ ${#JWT_SECRET} -ge 44 ]]             || die "JWT_SECRET kraći od 44 karaktera (= 32 bajta). Generiši: openssl rand -base64 64 | tr -d '\\n'"
    [[ "${JWT_SECRET}" != *"REPLACE_ME"* ]]  || die "JWT_SECRET sadrži REPLACE_ME."
    [[ ${#POSTGRES_PASSWORD} -ge 24 ]]       || die "POSTGRES_PASSWORD kraći od 24. Generiši: openssl rand -base64 24"
    [[ "${POSTGRES_PASSWORD}" != "${SLUG}-dev-only" ]]       || die "POSTGRES_PASSWORD je dev lozinka."
    [[ ${#ADMIN_INITIAL_PASSWORD} -ge 12 ]]  || die "ADMIN_INITIAL_PASSWORD kraći od 12."
    [[ "${ADMIN_INITIAL_PASSWORD}" != "Admin123!" ]]          || die "ADMIN_INITIAL_PASSWORD je default Admin123!."
    [[ "${ADMIN_INITIAL_PASSWORD}" != Dev*2026! ]]            || die "ADMIN_INITIAL_PASSWORD liči na dev lozinku (Dev…2026!)."

    log "Provera tajni prošla (domen: ${APP_DOMAIN})."
}
validate_secrets
[[ "${DEPLOY_VALIDATE_ONLY:-}" == "1" ]] && { log "DEPLOY_VALIDATE_ONLY=1 — kraj."; exit 0; }
[[ -f "${COMPOSE_FILE}" ]] || die "Ne postoji ${COMPOSE_FILE}. Da li si u /opt/apps/${SLUG}?"

TAR_ARG="${1:-}"
[[ -n "${TAR_ARG}" ]] || die "Upotreba: deploy.sh <${SLUG}-images-<tag>.tar | ${SLUG}-frontend-<tag>.tar>  (host ne build-uje iz gita)"
[[ -f "${TAR_ARG}" ]] || die "Tar '${TAR_ARG}' ne postoji."
TAR_FILE="$(realpath -- "${TAR_ARG}")"      # apsolutno PRE cd, da relativna putanja ne zaluta
PREV_TAG="${IMAGE_TAG:-}"                   # iz .env — trenutno deploy-ovana verzija (prazno pri prvom deploy-u)

# ─── 2) data/ podfolderi (jedini bind mount). data/postgres pravi sam postgres. ───
# Kontejnerski `app` je uid 1654 (rootless: subuid). Folderi moraju biti upisivi
# za njega: chown ako znamo host uid, inače 0770 (+ ručna provera, blueprint 09 §7).
APP_HOST_UID="${APP_HOST_UID:-}"
for d in "${REPO_ROOT}/data/logs/api" "${REPO_ROOT}/data/data-protection-keys" "${REPO_ROOT}/data/backups"; do
    mkdir -p "${d}"
    if [[ -n "${APP_HOST_UID}" ]]; then
        chown "${APP_HOST_UID}:${APP_HOST_UID}" "${d}" 2>/dev/null || warn "chown ${APP_HOST_UID} nije uspeo za ${d}"
    fi
    chmod 0770 "${d}"
done
chmod 0775 "${REPO_ROOT}/data/backups"      # backend samo čita ime poslednjeg backupa (stanje servera)
cd "${REPO_ROOT}"

# ─── 3) docker load; IMAGE_TAG iz tar-a (tar je izvor istine) ───
log "Učitavam image-e iz ${TAR_FILE}…"
LOAD_OUT="$(docker load -i "${TAR_FILE}")"; echo "${LOAD_OUT}"
BACKEND_TAG="$(echo "${LOAD_OUT}"  | sed -n "s|.*${SLUG}/backend:\([^ ]*\).*|\1|p"  | tail -1)"
FRONTEND_TAG="$(echo "${LOAD_OUT}" | sed -n "s|.*${SLUG}/frontend:\([^ ]*\).*|\1|p" | tail -1)"
[[ -n "${FRONTEND_TAG}" ]] || die "Tar ne sadrži ${SLUG}/frontend image."

if [[ -z "${BACKEND_TAG}" ]]; then
    # Frontend-only tar: backend se ne menja → pretaguj trenutnu verziju na novi tag,
    # da compose (koji traži ISTI tag za oba servisa) ima šta da pokrene.
    [[ -n "${PREV_TAG}" ]] || die "Tar ima samo frontend, a nema prethodno deploy-ovane verzije (IMAGE_TAG u .env je prazan). Prvi deploy traži pun tar."
    docker image inspect "${SLUG}/backend:${PREV_TAG}" >/dev/null 2>&1 \
        || die "Nema lokalnog image-a ${SLUG}/backend:${PREV_TAG} za pretagovanje."
    docker tag "${SLUG}/backend:${PREV_TAG}" "${SLUG}/backend:${FRONTEND_TAG}"
    BACKEND_TAG="${FRONTEND_TAG}"
    warn "Frontend-only deploy: backend ${PREV_TAG} pretagovan kao ${FRONTEND_TAG}."
fi
[[ "${BACKEND_TAG}" == "${FRONTEND_TAG}" ]] || die "Tagovi se ne slažu: backend=${BACKEND_TAG}, frontend=${FRONTEND_TAG}."
export IMAGE_TAG="${BACKEND_TAG}"
log "IMAGE_TAG: ${IMAGE_TAG}  (prethodno: ${PREV_TAG:-nema})"

# ─── 4) up -d ───
log "Pokrećem stack…"
compose up -d

# ─── 5) healthy ───
wait_healthy() {
    local svc="$1" timeout="${2:-90}" elapsed=0 cid status
    log "Čekam da '${svc}' postane healthy (timeout ${timeout}s)…"
    while true; do
        cid="$(compose ps -q "${svc}" 2>/dev/null || true)"
        if [[ -z "${cid}" ]]; then status="not-running"
        else status="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}no-healthcheck{{end}}' "${cid}" 2>/dev/null || echo unknown)"; fi
        case "${status}" in
            healthy)        log "${svc} je healthy."; return 0 ;;
            unhealthy)      die "${svc} je unhealthy — 'docker compose logs ${svc}'. Rollback: IMAGE_TAG=${PREV_TAG:-<stari>} docker compose up -d" ;;
            no-healthcheck) warn "${svc} bez healthcheck-a."; return 0 ;;
            *)              ;;
        esac
        (( elapsed >= timeout )) && die "Timeout — ${svc} nije healthy posle ${timeout}s."
        sleep 3; elapsed=$(( elapsed + 3 ))
    done
}
wait_healthy backend 90
wait_healthy frontend 60

# ─── 6) smoke iznutra: nginx → backend → baza, pa verzija ───
log "Smoke: frontend nginx → /api/v1/health/ready"
RESPONSE="$(compose exec -T frontend wget --quiet --output-document=- --timeout=10 http://127.0.0.1:8080/api/v1/health/ready || true)"
[[ "${RESPONSE}" == *'"status":"ready"'* ]] || die "Smoke pao. Odgovor: ${RESPONSE:-<prazno>}"
log "Smoke prošao: ${RESPONSE}"

VERZIJA_OCEKIVANA="${IMAGE_TAG%%-*}"
VERSION_JSON="$(compose exec -T frontend wget --quiet --output-document=- --timeout=10 http://127.0.0.1:8080/api/v1/version || true)"
if [[ "${VERSION_JSON}" == *"\"verzija\":\"${VERZIJA_OCEKIVANA}\""* ]]; then
    log "Verzija potvrđena: ${VERSION_JSON}"
else
    die "Verzija servera ne odgovara tagu ${IMAGE_TAG}. Odgovor /version: ${VERSION_JSON:-<prazno>}"
fi

# ─── 7) upiši IMAGE_TAG u .env (da ad-hoc compose komande rade) ───
if grep -qE '^IMAGE_TAG=' "${ENV_FILE}"; then
    sed -i "s|^IMAGE_TAG=.*|IMAGE_TAG=${IMAGE_TAG}|" "${ENV_FILE}"
else
    printf '\nIMAGE_TAG=%s\n' "${IMAGE_TAG}" >> "${ENV_FILE}"
fi

log "Deploy završen: ${SLUG} → ${IMAGE_TAG}. Spolja: https://${APP_DOMAIN}/ (Traefik)."
log "Proveri u browseru dijalog 'O aplikaciji' (${VERZIJA_OCEKIVANA}); zapiši u TODO.md; obriši tar: rm ${TAR_FILE}"
