# Faza 01 — Osnova: role, prava, podešavanja

**Cilj:** aplikacija zna četiri role i prava štikliranjem; Admin pravi naloge sa imenom i rolom i menja prava; postoje
sekcije, brojači i podešavanja na koja se oslanjaju sve sledeće faze.
**Pravila:** R1.4–R1.12, R12.1 (korisnici i prava).
**Uslov:** PREDLOG „četiri role i prava štikliranjem" (`docs/odluke.md`) odobren; posle odobrenja PREDLOG → odluka sa datumom.

## Odluke za ovu fazu

- **Prava idu u JWT** kao claim `pravo` (više vrednosti), isto kao rola. Promena prava revokuje refresh tokene korisnika
  (postojeći obrazac za promenu role), pa nova prava važe najkasnije za 15 min. Isti prihvaćen rizik kao za role (`SECURITY.md`).
- **Politika po pravu:** `RequirePravo:<pravo>` preko jednog `IAuthorizationHandler`-a, ne politika po roli. Rola određuje
  samo podrazumevani skup prava pri kreiranju naloga i promeni role.
- `pregledi.popunjavanje` može da ima samo Inženjer (R1.8) — guard u komandi, 422.

## Prava i podrazumevani skup (R1.5)

| Pravo | Inženjer | Supervizor | Direktor | Admin |
|---|---|---|---|---|
| `pregledi.popunjavanje` | ✔ | — | — | — |
| `analitika` | ✔ | ✔ | ✔ | ✔ |
| `overa` | — | ✔ | ✔ | — |
| `raspored.uredjivanje` | — | ✔ | ✔ | — |
| `odsustva.odobravanje` | — | ✔ | ✔ | — |
| `akcije.zatvaranje` | — | ✔ | ✔ | — |
| `sabloni.nacrt` | — | — | ✔ | ✔ |
| `sabloni.objava` | — | — | ✔ | — |
| `neradni_dani` | — | — | ✔ | ✔ |
| `korisnici` | — | — | — | ✔ |
| `podesavanja` | — | — | — | ✔ |

Čitanje pregleda, neispravnosti, uređaja i analitike je za sve ulogovane (R1.7) — politika `Authenticated`.

## Zadaci

### Backend
1. `Roles.cs`: `Inzenjer`, `Supervizor`, `Direktor`, `Admin` (uklanja se `Korisnik`); `Prava.cs` sa konstantama i
   `PodrazumevanaPrava(rola)`.
2. Migracija `Osnova`:
   - `AspNetUsers` + `ime_prezime text not null`;
   - `korisnik_pravo (korisnik_id, pravo)` PK oba;
   - `sekcija (id, sifra unique, naziv, redosled)` + seed 10, 20, 21, 30, 40, 50, 60, 70;
   - `brojac (vrsta, godina, poslednji)` PK vrsta+godina;
   - `podesavanje (kljuc pk, vrednost jsonb)` + seed podrazumevanih vrednosti (kraj radnog dana, 13:00 za DP, granica pauze 10 min,
     odstupanje sata, rok overe 24 h, „na dan PZ-a DP se ne radi", pragovi ponovljenog kvara 3/30, van funkcije 30 dana).
   - Postojeći korisnici sa rolom `Korisnik` prelaze u `Inzenjer` (migracija podataka; dev baza ima samo admina).
3. `IBrojacService.Sledeci(vrsta, godina)` — `SELECT … FOR UPDATE` u istoj transakciji; format `DP1-2026-0142`, `A-2026-0031`, `K-2026-0087`.
4. Korisnici: DTO i komande dobijaju `imePrezime` i `prava[]`; `PUT /korisnici/{id}/prava`; promena role vraća podrazumevana prava
   (Admin ih posle može menjati). Guardovi: poslednji Admin, samo Inženjer ima `pregledi.popunjavanje`.
5. `GET/PUT /podesavanja` (pravo `podesavanja`) — tipiziran DTO, ne slobodan JSON sa fronta.
6. Login/`me` vraćaju `prava[]`.
7. `GET /sekcije` (Authenticated).

### Frontend
1. `lib/permissions.ts`: tip `Rola` (4), labele sa dijakritikama, `imaPravo(user, pravo)`; `Zasticeno` prima pravo.
2. Korisnici: kolona „Ime i prezime", izbor role (4), dijalog „Prava" sa štikliranjem i „Vrati podrazumevana".
3. Ekran „Podešavanja" (Admin) — za sada samo vrednosti iz migracije; primaoci obaveštenja stižu u fazi 09.
4. Meni po pravima (`NAV visibleIf`); stavke kasnijih faza se ne prikazuju dok ne postoje.

### Testovi
- Unit: `BrojacTests` (po tipu i godini, nova godina kreće od 1, paralelni zahtevi ne dobijaju isti broj); podrazumevana prava po roli;
  guard „samo Inženjer popunjava".
- Integracioni: 401/403 za `/korisnici/{id}/prava`, `/podesavanja`, `/sekcije`; promena prava revokuje refresh.
- Frontend: guard rute po pravu; dijalog prava.

### Docs
`docs/02` (nove tabele), `docs/03` (rute), `docs/04` (role, prava, matrica iznad), `docs/user/01-prijava-i-role.md`,
`docs/user/05-upravljanje-korisnicima.md`, CHANGELOG.

## Gotovo kada
Admin napravi po jedan nalog za svaku rolu, promeni nekome prava, a taj korisnik posle ponovne prijave vidi tačno
dozvoljene stavke menija i dobija 403 na ostalim rutama; svi testovi zeleni, `npm run lint` 0.
