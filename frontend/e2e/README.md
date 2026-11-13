# E2E testovi (Playwright)

Testovi kroz pravi pregledač nad pravim API-jem i praznom bazom:

| Fajl | Šta proverava |
|---|---|
| `priprema.setup.ts` | Pravi probne naloge, uvozi šablone (`docs/sabloni/sabloni-za-uvoz.xlsx`), zakazuje PZ za danas i prijavljuje svaku rolu jednom |
| `prijava.spec.ts` | Pogrešna lozinka javlja grešku; posle prijave korisnik ide tamo gde je krenuo |
| `role.spec.ts` | Meni Admina i Direktora; ruta bez prava vraća na Početnu |
| `pregled.spec.ts` | Inženjer na telefonu: započne PZ, popuni i preda bez interneta, a kad se veza vrati sve se pošalje; Supervizor vidi „Overi“ |

U CI-ju ih pokreće posao **E2E (Playwright)** (`.github/workflows/ci.yml`) na svaki PR. Kad padne, izveštaj, snimci ekrana
i log API-ja su u artefaktu `e2e-izvestaj`.

## Lokalno

1. Napravite praznu bazu (npr. `tpm_e2e` na dev klasteru) i pokrenite API nad njom sa izabranom početnom lozinkom admina:
   `ASPNETCORE_ENVIRONMENT=Development`, `ConnectionStrings__Default=…Database=tpm_e2e…`, `Admin__InitialPassword=<lozinka>`,
   pa `dotnet run --project backend/src/TpmPregledi.Api`.
2. U `frontend/`: postavite `E2E_ADMIN_LOZINKA` (ista kao gore) i `E2E_LOZINKA` (bilo koja jaka lozinka za probne naloge),
   jednom `npx playwright install chromium`, pa `npm run e2e`.

Priprema zakazuje PZ za danas, pa se za ponovno pokretanje baza pravi iznova. Prijava je ograničena na 10 u minuti po
IP adresi, zato se svaka rola prijavljuje samo jednom, a sačuvanu prijavu (`e2e/.stanje/`) koristi tačno jedan test.
