# Sigurnost — TPM pregledi

## Ovaj repo ne sme biti javan

Sadrži interne hostove, putanje i topologiju (`docs/deploy/`, `docker-compose.yml`). Pre bilo kakvog
objavljivanja ti delovi moraju da se izmeste. Kredencijali nikad nisu u repou (provera: `git ls-files | grep -Ei 'env$|pem$|ppk$'` prazno).

## Model pretnji (kratko)

Interni poslovni alat dostupan samo preko Elixir VPN-a, iza IT-jevog Traefik-a. Korisnici su zaposleni sa ličnim nalozima.
Štitimo: integritet podataka (ko je šta promenio — audit log), tajnost lozinki i tokena, dostupnost (health, backup).
Ne branimo se od: javnog interneta (nema izloženosti), DDoS-a, naprednih napadača unutar mreže.

## Implementirane mere

Po Elixir blueprintu, dokument 12 (50 stavki). TpmPregledi ih sadrži sve koje se odnose na kod i šablone.
Pregled pre go-live: `__/50` (datum, ko).

## Prihvaćeni rizici

| Rizik | Zašto prihvaćen | Ko / kad |
|---|---|---|
| Distribuirani brute force po korisničkom imenu (rate limit je po IP-ju; nema per-username limitera) | lockout 5/10 min po nalogu + VPN; mala površina | vlasnik projekta, pre go-live |
| In-memory rate limiter se resetuje pri restartu | jedan instance, restart je redak | isto |
| Timing side-channel pri prijavi | zanemarljivo u internom okruženju | isto |
| TOCTOU u guardovima „poslednji admin" (read-then-mutate bez row lock-a) | dva admina istovremeno deaktiviraju jedan drugog — praktično nemoguće | isto |
| DB korisnik aplikacije je vlasnik baze (nema least-privilege runtime role) | baza samo u internoj docker mreži | isto |

## Rotacija tajni

| Tajna | Kako | Kad |
|---|---|---|
| `JWT_SECRET` | novi `openssl rand -base64 64 \| tr -d '\n'` u `.env`, `docker compose up -d backend`; svi korisnici se izloguju | godišnje ili posle incidenta |
| `POSTGRES_PASSWORD` | `ALTER USER` + `.env` + restart backenda | na zahtev IT-a |
| Admin lozinka | iz UI (korisnički meni → Promena lozinke) | posle prvog deploy-a i po potrebi |
| SSH/RDP pristup hostu | Passbolt, IT politika | po IT-u |

## CVE triage

| Paket / advisory | Status | Obrazloženje |
|---|---|---|
| — | — | — |

## Kontakt

Vlasnik projekta (vidi `README.md`). Prijave bezbednosnih problema direktno, ne kroz javne issue-e.

## Istorija pregleda

| Datum | Ko | Rezultat |
|---|---|---|
| | | inicijalni pregled po blueprintu 12 |
