# Zahtevi ka Elixir IT-u

> Šta tražimo od IT-a da bi aplikacija mogla u produkciju, sa statusom. Kontakt: vlasnik projekta.

| # | Zahtev | Zašto | Status |
|---|---|---|---|
| 1 | Potvrda produkcionog kratkog imena i domena `tpm-pregledi.elixirgroup.rs` (DNS → Traefik) | Traefik `Host()` pravilo, CORS, AllowedHosts | ⏳ |
| 2 | Folder `/opt/apps/tpm-pregledi/` i app nalog u `docker` grupi | deploy bez sudo-a | ⏳ |
| 3 | Potvrda imena Traefik mreže (`proxy-net`) i entrypoint-a (`websecure`, wildcard TLS, 80→443) | compose labele | ⏳ |
| 4 | Passbolt pristup za tim projekta | razmena `.env` tajni i SSH lozinke | ⏳ |
| 5 | Backup `data/backups/` van hosta (NFS/CIFS/rsync), dnevno | oporavak posle gubitka hosta | ⏳ |
| 6 | (opciono) cron za `deploy/scripts/backup.sh` ako app nalog ne sme da ima crontab | dnevni dump | ⏳ |
| 7 | Health za IT monitoring: `GET https://tpm-pregledi.elixirgroup.rs/api/v1/health/ready` → `{"status":"ready"}` | nadzor | — |

## Šta NE tražimo

- Izmene Traefik-a, LDAP/AD integraciju, izlaz hosta ka internetu (image dolazi kao tar), registry.

## Istorija

| Datum | Šta | Ko |
|---|---|---|
| | | |
