# 04 — Role i pristupna prava

> Stvarno stanje koda. Standard: Elixir blueprint 07. **Backend je autoritativan; frontend ogleda** (`Zasticeno` na rutama, `NAV visibleIf`).

## 1. Role

| Rola (kod/žica) | Labela u UI | Namena |
|---|---|---|
| `Admin` | Administrator | sve, uključujući korisnike, audit log, stanje servera, izmene šifarnika |
| `Korisnik` | Korisnik | čita šifarnike i radi u aplikaciji; ne administrira |

Tačno jedna rola po korisniku (`rola` u `KorisnikDto`; login/me vraćaju `roles: []` sa jednim elementom). Konstante: `Domain/Identity/Roles.cs`.

## 2. Politike (`Program.cs`)

| Politika | Role |
|---|---|
| `RequireAdmin` | Admin |
| `RequireKorisnik` | Admin, Korisnik |
| `Authenticated` | bilo koji ulogovan |

## 3. Matrica

| Resurs / akcija | Korisnik | Admin | Politika endpointa |
|---|---|---|---|
| Prijava, refresh, odjava, `/me`, promena sopstvene lozinke | ✔ | ✔ | anonimno / Authenticated |
| `/version`, `/health*` | ✔ (i anonimno) | ✔ | anonimno |
| Kategorije — čitanje | ✔ | ✔ | RequireKorisnik |
| Kategorije — kreiranje, izmena, deaktivacija, aktivacija | — | ✔ | RequireAdmin |
| Korisnici — sve | — | ✔ | RequireAdmin |
| Audit log | — | ✔ | RequireAdmin |
| Stanje servera | — | ✔ | RequireAdmin |

Frontend: `Korisnik` vidi u meniju samo Kategorije (bez dugmeta „Nova kategorija" i akcija); `/admin/*`, `/audit-log`, `/stanje-servera` ga preusmeravaju na landing (`/kategorije`).

## 4. Zaštite pri upravljanju korisnicima (`KorisnikGuards.cs`, sve → 422 `greske[]`)

1. „Ne možete promeniti sopstvenu rolu."
2. „Ne možete deaktivirati sopstveni nalog."
3. „Ne možete ukloniti rolu Administrator poslednjem aktivnom administratoru."
4. „Ne možete deaktivirati poslednjeg aktivnog administratora."

Redosled pri promeni role: prvo dodaj novu, pa skini staru. Poznato ograničenje: read-then-mutate bez row lock-a (TOCTOU) — prihvaćen rizik (`SECURITY.md`).
Guardovi 3 i 4 su kroz API dostižni samo sa još važećim JWT-om deaktiviranog admina (do 15 min): aktivan akter koji nije cilj znači da postoje
bar dva aktivna admina. Ostaju kao odbrana u dubinu i tako su integraciono testirani.

## 5. Lozinke i sesija

Politika: min 8, cifra, veliko i malo slovo. Lockout 5 pokušaja / 10 min. Neuspešna prijava uvek vraća istu poruku i piše `LOGIN_FAIL` u audit.
Access token 15 min; refresh 7 dana sa rotacijom; reset lozinke (admin), promena lozinke, deaktivacija i promena role revokuju sve refresh tokene.
Korisničko ime je nepromenljivo; korisnici se ne brišu nego deaktiviraju.

## 6. Inicijalni admin

`Admin__InitialUsername/Email/Password` iz `.env`; seed samo ako nijedan admin ne postoji; odbija praznu ili `Admin123!`. Posle prvog deploy-a
admin menja lozinku (korisnički meni → Promena lozinke).

## 7. Dodavanje nove role

`Roles.cs` → politika u `Program.cs` → `.RequireAuthorization` na rutama → ova matrica → `AuthorizationPolicyTests` → frontend `lib/permissions.ts`
(`type Rola`, labela, `can…`) → `NAV visibleIf` → `Zasticeno` → radio u `KorisnikForm` → testovi → `docs/user/01-prijava-i-role.md`. Bez migracije podataka.
