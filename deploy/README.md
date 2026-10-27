# Deploy — TPM pregledi

> Operativno uputstvo za produkciju. Standard: Elixir blueprint 17. Pristup serveru i zahtevi ka IT-u: `docs/deploy/`.

## Sadržaj
1. Okruženje
2. Arhitektura
3. Doprema image-a
4. Prvi deploy
5. Update
6. Backup
7. Restore
8. Logovi
9. Rollback

## 1. Okruženje

| | |
|---|---|
| Docker host | Elixir deljeni docker host (ime i adresa: `docs/deploy/server-environment.md`) |
| Folder | `/opt/apps/tpm-pregledi/` |
| Ulaz | Traefik na deljenoj mreži `proxy-net`; TLS i 80→443 su na Traefik-u; domen `tpm-pregledi.elixirgroup.rs` |
| Interna mreža | `tpm-pregledi-internal` (backend + db, neizloženi) |
| Tajne | `/opt/apps/tpm-pregledi/.env` (`chmod 600`), razmena kroz Passbolt |
| Pristup | VPN → RDP Jump server → SSH kao app nalog; sesije se snimaju (`docs/deploy/pristup.md`) |

## 2. Arhitektura

```
[Traefik] :443 ── Host(tpm-pregledi.elixirgroup.rs) ──► frontend (nginx :8080) ──► backend (:8080) ──► db (:5432)
           proxy-net                                │ tpm-pregledi-internal ──────────────────────────┘
```
Samo `frontend` je na `proxy-net`; `/api/*` ide kroz njegov nginx na alias `tpm-pregledi-backend`.

## 3. Doprema image-a

Nema registry-ja i host ne build-uje. Image se build-uje na dev mašini i prenosi kao tar:

```bash
# dev mašina (Docker Desktop), koren repoa, HEAD tagovan vX.Y.Z
bash deploy/scripts/export-images.sh              # → tpm-pregledi-images-X.Y.Z-<sha>.tar
bash deploy/scripts/export-images.sh --frontend   # samo frontend (kad backend nije menjan)
```
Tar se prenosi SFTP-om kroz Jump server (MobaXterm) u `/opt/apps/tpm-pregledi/`.

## 4. Prvi deploy

Preduslovi od IT-a (status u `docs/deploy/sysadmin-tasks.md`): folder, app nalog u `docker` grupi (**nova SSH sesija** posle dodavanja), domen, `proxy-net`.

```bash
# lokalno: bundle sa fajlovima koje host treba (bez tajni)
tar czf tpm-pregledi-server-bundle.tar.gz docker-compose.yml .env.example README.md deploy/
# popunjen .env čuvaj u zasebnom lokalnom folderu za prenos, nikad u repou

# na hostu
cd /opt/apps/tpm-pregledi
tar -xzf tpm-pregledi-server-bundle.tar.gz
mv tpm-pregledi.env .env && chmod 600 .env             # popunjen .env (sve vrednosti u jednostrukim navodnicima)
chmod +x deploy/scripts/*.sh
DEPLOY_VALIDATE_ONLY=1 deploy/scripts/deploy.sh   # samo provera tajni
deploy/scripts/deploy.sh tpm-pregledi-images-X.Y.Z-<sha>.tar
docker compose ps                                 # 3× healthy
```
Zatim u browseru (sa Jump-a): prijava `admin`, **promena lozinke** (korisnički meni → Promena lozinke), „O aplikaciji" pokazuje `X.Y.Z`.
Obriši tar na hostu i lokalno i lokalni popunjeni `.env`. Upiši u `TODO.md`.

## 5. Update

1. Ako verzija donosi migraciju: `deploy/scripts/backup.sh` **prvo**.
2. Prenesi novi tar; `deploy/scripts/deploy.sh <tar>`.
3. Proveri „O aplikaciji"; obriši tar; upiši u `TODO.md`.

`deploy.sh` posle uspeha upisuje `IMAGE_TAG` u `.env`, pa `docker compose ps|logs|exec` rade bez dodatnih promenljivih.

## 6. Backup

```bash
deploy/scripts/backup.sh          # data/backups/tpm-pregledi-YYYY-MM-DD-HHMM.pgcustom.gz (+ dp-keys), retencija 14 dana
# cron (ako IT dozvoli): 0 2 * * * /opt/apps/tpm-pregledi/deploy/scripts/backup.sh >> /opt/apps/tpm-pregledi/data/backups/backup.log 2>&1
```
Kopija van hosta je IT-jev zadatak. „Stanje servera" prikazuje poslednji backup.

## 7. Restore

```bash
docker compose stop backend
gunzip -c data/backups/tpm-pregledi-<datum>.pgcustom.gz | docker compose exec -T db pg_restore -U tpm-pregledi -d tpm-pregledi --clean --if-exists
docker compose start backend
```
Ako aplikacija šifruje podatke Data Protection-om: vrati i `data/data-protection-keys` iz `dp-keys-*.tgz` pre starta.
Restore test se radi pre go-live i kvartalno.

## 8. Logovi

```bash
docker compose logs -f backend --tail=200
tail -f data/logs/api/log-$(date +%Y%m%d).txt
grep '<traceId iz greške>' data/logs/api/log-*.txt
```
U aplikaciji: Audit log i Stanje servera (Admin).

## 9. Rollback

```bash
IMAGE_TAG=<prethodni-tag> docker compose up -d        # image-i ostaju u lokalnom kešu
```
Ako je nova verzija donela migraciju: `stop backend` → restore dump-a od pre deploy-a → `IMAGE_TAG=<stari> up -d`.
Zapisati u `TODO.md` i `CHANGELOG.md`.
