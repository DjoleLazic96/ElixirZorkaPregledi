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
| 8 | Aplikacija u Entra ID sa aplikacijskom dozvolom **Mail.Send** (Microsoft Graph) i sanduče pošiljaoca → `GRAPH_*` u `.env` | mejl obaveštenja (do tada samo zvono i push) | ⏳ |
| 9 | Izlaz backend kontejnera na internet, samo HTTPS (443) ka: `fcm.googleapis.com`, `updates.push.services.mozilla.com` (Web Push na telefone), `login.microsoftonline.com`, `graph.microsoft.com` (mejl) | obaveštenja na telefonu i mejlom (R9.1) | ⏳ |
| 7 | Health za IT monitoring: `GET https://tpm-pregledi.elixirgroup.rs/api/v1/health/ready` → `{"status":"ready"}` | nadzor | — |

## Šta NE tražimo

- Izmene Traefik-a, LDAP/AD integraciju, registry; izlaz ka internetu samo ka adresama iz #9 (image dolazi kao tar).

## Istorija

| Datum | Šta | Ko |
|---|---|---|
| | | |
