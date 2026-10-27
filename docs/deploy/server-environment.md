# Produkciono okruženje

> Popuniti konkretne vrednosti kad ih IT da. Ne upisivati lozinke — one idu u Passbolt.

## Host

| | Vrednost |
|---|---|
| Ime hosta | <!-- npr. DOCKER-XX-01.elixirgroup.rs --> |
| Adresa | <!-- interna IP, dostupna samo preko VPN-a --> |
| OS / Docker | <!-- popuniti po `docker info` na hostu --> |
| App nalog | <!-- ime naloga, član `docker` grupe; ograničen na app folder --> |
| Folder | `/opt/apps/tpm-pregledi/` |
| Deljenje hosta | **deljen** sa drugim aplikacijama — zato prefiksirani mrežni alias i bez `ports:` |

## Pravila IT-a (obavezna)

- `docker-compose.yml`, `.env` (van gita), `.env.example`, `README.md` i `data/` u app folderu; `data/` je **jedini** bind mount.
- Ulaz samo kroz Traefik: mreža `proxy-net`, labele `Host()`, `websecure`, `tls=true`; nema `ports:`.
- `restart` politika na svakom servisu; image-i sa eksplicitnom verzijom, bez `latest`.
- Nema registry-ja ni CI/CD deploy-a: image dolazi kao tar preko SFTP-a.
- Pun FQDN svuda (`tpm-pregledi.elixirgroup.rs`).
- SSH sesije se snimaju; kredencijali kroz Passbolt.

## Mreže i servisi

| Servis | Mreže | Izloženost |
|---|---|---|
| `frontend` | `proxy-net`, `tpm-pregledi-internal` | Traefik → :8080 |
| `backend` (alias `tpm-pregledi-backend`) | `tpm-pregledi-internal` | samo frontend nginx |
| `db` | `tpm-pregledi-internal` | samo backend |

## `data/` folder

| Putanja | Sadržaj | Vlasnik / mode |
|---|---|---|
| `data/postgres` | baza | postgres kontejner |
| `data/logs/api` | Serilog dnevni fajlovi (30 dana) | upisiv za uid 1654 (`0770` + chown ili `0777` na rootless hostu) |
| `data/data-protection-keys` | ASP.NET Data Protection ključevi — u backup-u | isto |
| `data/backups` | `backup.sh` izlaz, retencija 14 dana | `0775` (backend čita ime poslednjeg fajla) |

## Backup van hosta

<!-- Gde IT kopira data/backups (NFS/CIFS/rsync), koliko često, ko proverava. -->
