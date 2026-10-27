# Pristup serveru i onboarding

> Za novog programera na projektu. Konkretna imena hostova i naloga popunjava vlasnik projekta; lozinke su u Passbolt-u.

## Šta ti treba (traži od vlasnika projekta / Elixir IT-a)

| Šta | Ko daje | Napomena |
|---|---|---|
| GitHub pristup repou projekta i `elixir-blueprint` | vlasnik projekta | privatni repoi |
| VPN nalog (Cisco AnyConnect, profil Elixir) + MFA | Elixir IT | bez VPN-a ništa interno nije dostupno |
| RDP nalog na Jump server (AD nalog) | Elixir IT | sesije se snimaju |
| Passbolt pristup | Elixir IT | tu su `.env` tajne, SSH lozinka app naloga |
| Docker Desktop (WSL2) na dev mašini | ti / IT politika | za build image-a i integracione testove |

## Put do servera

```
dev mašina ──VPN──► RDP na Jump server ──MobaXterm (SSH + SFTP)──► docker host, kao app nalog, cd /opt/apps/tpm-pregledi
```

- Nema direktnog SSH-a sa dev mašine (ni `scp`/`rsync`). Fajlovi idu kroz MobaXterm SFTP panel na Jump-u.
- Na Jump-u se otvara **samo** folder sa fajlovima za prenos (tar, bundle). Interne beleške i kredencijali se ne otvaraju u snimanoj sesiji.
- DLP politika može da prijavi velike fajlove (~100+ MB tar): pošalji sa obrazloženjem, ne zaobilazi deljenjem ili preimenovanjem; šalji samo promenjeni image (`export-images.sh --frontend`).
- RDP: ako `\\tsclient\C` deljenje diska ne radi, **Sign out** pa ponovo (ne samo zatvoriti prozor).

## Pravila ponašanja na hostu

- Ništa na hostu bez odobrenja vlasnika projekta; svaka akcija se zapisuje u `TODO.md` (datum, komanda, rezultat).
- Samo komande iz `deploy/README.md`. Ne menjati compose/`.env` ručno van postupka.
- Po završetku: obrisati tar-ove, zatvoriti sesiju.

## Konkretno za ovaj projekat

| | |
|---|---|
| Jump server | <!-- ime --> |
| Docker host | <!-- ime, vidi server-environment.md --> |
| App nalog | <!-- ime --> |
| Passbolt folder | <!-- putanja --> |
| Kontakt u IT-u | <!-- ime, mejl --> |
