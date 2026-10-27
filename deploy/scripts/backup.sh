#!/usr/bin/env bash
# =============================================================================
# tpm-pregledi — backup baze (+ Data Protection ključeva) sa retencijom. Elixir blueprint 17 §7.
# =============================================================================
# Na docker hostu, iz /opt/apps/tpm-pregledi:
#   deploy/scripts/backup.sh                  # ručno, OBAVEZNO pre deploy-a koji donosi migraciju
#   0 2 * * * /opt/apps/tpm-pregledi/deploy/scripts/backup.sh >> /opt/apps/tpm-pregledi/data/backups/backup.log 2>&1
#
# Rezultat: data/backups/tpm-pregledi-YYYY-MM-DD-HHMM.pgcustom.gz (+ dp-keys-YYYY-MM-DD-HHMM.tgz)
# Retencija: RETENTION_DAYS (default 14). Kopiju VAN hosta radi IT (docs/deploy/sysadmin-tasks.md).
#
# Restore (stop backend prvo!):
#   docker compose stop backend
#   gunzip -c data/backups/X.pgcustom.gz | docker compose exec -T db pg_restore -U <user> -d <db> --clean --if-exists
#   docker compose start backend
# =============================================================================
set -euo pipefail

SLUG="tpm-pregledi"
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_ROOT="$( cd "${SCRIPT_DIR}/../.." && pwd )"
ENV_FILE="${REPO_ROOT}/.env"
BACKUP_DIR="${REPO_ROOT}/data/backups"
RETENTION_DAYS="${RETENTION_DAYS:-14}"
STAMP="$(date +%F-%H%M)"

[[ -f "${ENV_FILE}" ]] || { echo "!! Nema ${ENV_FILE}" >&2; exit 1; }
# .env bez `source` (vrednosti sa specijalnim znakovima) — isti parser kao deploy.sh
while IFS= read -r line || [[ -n "${line}" ]]; do
    [[ "${line}" =~ ^[[:space:]]*$ || "${line}" =~ ^[[:space:]]*# ]] && continue
    key="${line%%=*}"; val="${line#*=}"; key="${key//[[:space:]]/}"
    if   [[ "${val}" =~ ^\'(.*)\'$ ]]; then val="${BASH_REMATCH[1]}"
    elif [[ "${val}" =~ ^\"(.*)\"$ ]]; then val="${BASH_REMATCH[1]}"; fi
    printf -v "${key}" '%s' "${val}"; export "${key}"
done < "${ENV_FILE}"
: "${POSTGRES_USER:?}" "${POSTGRES_DB:?}"
[[ -n "${IMAGE_TAG:-}" ]] || { echo "!! IMAGE_TAG u .env je prazan — stack još nije deploy-ovan, nema šta da se bekapuje." >&2; exit 1; }

mkdir -p "${BACKUP_DIR}"; chmod 0775 "${BACKUP_DIR}"
cd "${REPO_ROOT}"

OUT="${BACKUP_DIR}/${SLUG}-${STAMP}.pgcustom.gz"
echo "==> $(date -Is) pg_dump → ${OUT}"
# Pod pipefail-om neuspeh pg_dump-a ne sme da ostavi polovičan .gz koji izgleda kao backup.
if ! docker compose --env-file "${ENV_FILE}" exec -T db \
        pg_dump -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" --format=custom | gzip > "${OUT}"; then
    rm -f "${OUT}"; echo "!! pg_dump pao — backup NIJE napravljen" >&2; exit 1
fi
chmod 600 "${OUT}"
gzip -t "${OUT}" || { rm -f "${OUT}"; echo "!! Arhiva je oštećena" >&2; exit 1; }
VELICINA="$(stat -c %s "${OUT}")"
(( VELICINA > 1024 )) || { rm -f "${OUT}"; echo "!! Dump je sumnjivo mali (${VELICINA} B)" >&2; exit 1; }
echo "==> $(du -h "${OUT}" | cut -f1) OK"

# Data Protection ključevi — bitni ako aplikacija šifruje nešto u bazi (npr. Azure secret).
if [[ -d "${REPO_ROOT}/data/data-protection-keys" ]] && [[ -n "$(ls -A "${REPO_ROOT}/data/data-protection-keys" 2>/dev/null)" ]]; then
    tar czf "${BACKUP_DIR}/dp-keys-${STAMP}.tgz" -C "${REPO_ROOT}/data" data-protection-keys
    chmod 600 "${BACKUP_DIR}/dp-keys-${STAMP}.tgz"
fi

# Retencija (čuva se poslednjih RETENTION_DAYS dana)
find "${BACKUP_DIR}" -name "${SLUG}-*.pgcustom.gz" -mtime "+${RETENTION_DAYS}" -delete
find "${BACKUP_DIR}" -name "dp-keys-*.tgz"          -mtime "+${RETENTION_DAYS}" -delete
echo "==> Gotovo. Kopija van hosta: IT (docs/deploy/sysadmin-tasks.md)."
