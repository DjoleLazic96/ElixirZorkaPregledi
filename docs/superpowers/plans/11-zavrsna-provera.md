# Faza 11 — Završna provera i isporuka

**Cilj:** test prikaza za svaki ekran, rolu, širinu i stanje veze; dokumentacija iz spec §11; check-lista za telefon; paket i lista za
puštanje u rad. **Pravila:** R10.8–R10.9, spec §8.1, §10–§11, R14.2.

## Šta je urađeno

- **Test prikaza** (`frontend/scripts/provera-prikaza.js`, rezultat u `docs/deploy/test-prikaza.md`): bočno pomeranje stranice i radnog
  dela, elementi van ekrana, kartice i dijaloga, tabela koja se pomera na ≥ 1366 px, dugmad < 44 px na telefonu. Pokrenuto za 4 role ×
  375/768/1366/1920 px, bez interneta za Inženjera (375, 1366) i Supervizora (375); dijalozi posebno na 375 px. Lokalni probni nalozi:
  `scripts/dev-probni-nalozi.py`. Playwright nije uveden (nova zavisnost i preuzimanje pregledača); skripta radi u svakom Chrome-u.
- **Ispravke prikaza:** `PageHeader` i `Pagination` se prelamaju; arhiva pregleda tabela od 1280 px; dugmad tabele korisnika se prelamaju;
  na telefonu 44 px za `Button`, `Select`, `DatePicker` (i „obriši datum“), `Tabs`, paginaciju, „?“ uputstva, „Zatvori“ u `Modal`-u, stavke
  kalendara; kartice umesto tabela za preglede uređaja, ispunjenje plana i istorijat šablona.
- **Povratak posle prijave:** neprijavljen korisnik koji otvori rutu (npr. `/qr/{token}` sa nalepnice) posle prijave dolazi na tu rutu
  (`features/auth/povratak.ts`, samo unutrašnje adrese).
- **Produkcija:** CSP `img-src … blob:` (slike se prikazuju kroz API sa tokenom kao blob — bez toga u produkciji ne bi radile); `no-cache` za
  `sw.js`, `push-sw.js`, `registerSW.js`, `manifest.webmanifest`, `workbox-*.js`; IT zahtev #9 (izlaz ka push i Graph servisima).
- **Dokumentacija:** `docs/05-pravila-pregleda.md` (aktivno vreme, kvar, istorija uređaja, prenos, ispunjenje plana, rok overe i akcije —
  sa brojevima); `docs/01-architecture.md` §10; `docs/user/README.md` (četiri role, ko šta radi); `07-sta-je-novo.md`;
  `docs/deploy/provera-telefona.md`; `deploy/README.md` §0 (puštanje u rad).

## Šta ostaje čoveku (ne radi AI — CLAUDE.md, pravilo 8)

IT zahtevi #1–#9, verzija i tag, `export-images.sh` (Docker), prvi deploy, uvoz šablona, nalozi i podešavanja, provera na telefonu na
produkcionoj adresi, backup i restore test, obuka korisnika. Integracioni testovi (Testcontainers) traže Docker — pokrenuti pre izdavanja
verzije (`cd backend; dotnet test`). Nginx izmene se proveravaju pri prvom deploy-u (`docker compose exec frontend nginx -t`, slike u
detalju pregleda, `curl -I /sw.js` → `Cache-Control: no-cache`).
