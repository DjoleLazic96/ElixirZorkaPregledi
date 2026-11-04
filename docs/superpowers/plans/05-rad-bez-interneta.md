# Faza 05 — Rad bez interneta

**Cilj:** inženjer radi ceo pregled bez signala; telefon sam šalje urađeno kad se veza vrati. **Pravila:** R10.1–R10.7, R1.11, R5.1, R6.8.
**Uslov:** odobreni „PWA“, „Zapamti me“, „biblioteke“ (2026-11-04).

## Odluke

- **PWA** (`vite-plugin-pwa` 1.3.0): manifest i service worker samo za app shell (`navigateFallback`); `/api` se ne kešira.
- **IndexedDB** (`dexie` 4.0.11, `src/offline/sinhronizacija.ts`): `pregledi` (paket), `red` (operacije), `slike`, `odbijene`, `meta`
  (ETag, poslednja sinhronizacija). Token se tu ne čuva.
- **Paket** `GET /moj-paket` (ETag, 304): dodeljeni pregledi za danas + 7 dana i započeti; planiran nosi važeću verziju (`verzijaId`).
- **Početak sa telefona** je operacija `zapocni` (`verzijaId`): vreme početka = vreme sa telefona, verzija = ona po kojoj je telefon
  popunjavao (R10.5), pregled se vezuje za `uredjajKlijenta` (ID instalacije u `localStorage`). Drugi telefon dobija odbijenu
  operaciju „Pregled je započet na drugom telefonu…“.
- **Lokalna primena** (`src/offline/lokalno.ts`, čista funkcija): radnja se odmah vidi; posle novog paketa neposlate operacije se
  ponovo primene. Predaja bez interneta: lokalni status „Predato – čeka slanje“.
- **Sporan unos** (`sporan_unos`): operacije posle prebacivanja ili poništavanja se čuvaju; odluka je faza 06.
- **Sat telefona** (R5.1): odstupanje veće od podešavanja → događaj `odstupanje_sata` (vidi kontrolor, faza 06).
- **Zapamti me** (R1.11): `refresh_token.dugotrajan`, 180 dana (`Jwt:RefreshTokenDaysZapamti`), rotacija zadržava rok. Odjava je
  dozvoljena tek kad red bude prazan.
- Slanje: odmah posle radnje, na svakih minut i na događaj `online`; traka „Bez interneta“ ispod zaglavlja sa brojem neposlatih
  promena i slika; „Poslednja sinhronizacija: HH:MM“ na ekranu pregleda.
