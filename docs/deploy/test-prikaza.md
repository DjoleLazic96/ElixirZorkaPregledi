# Test prikaza (spec §8.1, R10.8–R10.9)

> Svaki ekran × svaka rola × 375 / 768 / 1366 / 1920 px × sa i bez interneta: stranica se ne pomera bočno, ništa ne izlazi iz ekrana,
> kartice ni dijaloga, a na telefonu su dugmad najmanje 44 × 44 px. Automatski deo je skripta `frontend/scripts/provera-prikaza.js`;
> ručni deo je pregled izgleda (poravnanje, prelom teksta, čitljivost).

## Kako se pokreće

1. Lokalno: API, `npm run dev`, probni nalozi (`python scripts/dev-probni-nalozi.py`; nalozi i lozinka su u `CLAUDE.md`).
2. Chrome → DevTools → **Toggle device toolbar**, širina 375 / 768 / 1366 / 1920.
3. U konzoli: `await import('/scripts/provera-prikaza.js')`, pa `await proveriPrikaz(['/', '/kalendar', '/pregledi', …])`.
   Rezultat `{}` znači da nema problema; inače spisak po ruti („stranica se pomera bočno“, „van ekrana: …“, „malo dugme 32×32: …“).
4. Bez interneta: DevTools → Network → **Offline** (ili ugašen API uz `offline` događaj), pa ista provera.
5. Dijalozi (uputstvo, nova akcija, QR, predlog izmene, zvono, „Obaveštenja na telefonu“) se otvaraju ručno, pa se proverava da su
   unutar ekrana i da je „Zatvori“ najmanje 44 × 44 px na telefonu.

## Rezultat — 2026-11-04 (verzija 0.1.0 + faze 01–11)

Rute: `/`, `/kalendar`, `/pregledi`, `/pregledi/{id}`, `/pregledi/{id}?uredjaj=`, `/pregled/{id}/popuni`, `/akcije`, `/akcije/{id}`,
`/neispravnosti`, `/neispravnosti/{id}`, `/analitika`, `/raspored`, `/moja-odsustva`, `/sabloni` (+ tabovi), `/sabloni/predlozi`,
`/sabloni/{tip}/nacrt`, `/uredjaji`, `/uredjaji/{id}`, `/neradni-dani`, `/admin/korisnici`, `/admin/podesavanja`, `/audit-log`,
`/stanje-servera` — svaka rola vidi one koje joj prava dozvoljavaju (ostale je vraćaju na Početnu).

| Rola | 375 | 768 | 1366 | 1920 | Bez interneta |
|---|---|---|---|---|---|
| Inženjer | ✔ | ✔ | ✔ | ✔ | ✔ 375 i 1366 (Početna, Kalendar, popunjavanje iz lokalnog paketa, Pregledi, Akcije, uređaj) |
| Supervizor | ✔ | ✔ | ✔ | ✔ | ✔ 375 (Početna, Kalendar, Raspored, Pregledi, uređaj) |
| Direktor | ✔ | ✔ | ✔ | ✔ | — (isti ekrani kao Supervizor) |
| Admin | ✔ | ✔ | ✔ | ✔ | — |

Ispravljeno tokom provere:
- paginacija i zaglavlje stranice prelaze u novi red (bili su van ekrana na 375 px — Pregledi, Audit log, Raspored);
- arhiva pregleda prikazuje tabelu tek od 1280 px (na 768 px sa bočnim menijem tabela nije stajala), ispod su kartice;
- dugmad u tabeli korisnika prelaze u novi red (tabela se pomerala bočno na 1366 px);
- na telefonu: dugmad, padajuće liste, izbor datuma, tabovi, paginacija, „?“ uputstva, „Zatvori“ u dijalozima i stavke kalendara su 44 px;
- na telefonu kartice umesto tabela: pregledi uređaja, ispunjenje plana, istorijat šablona.

Poznata odstupanja (svesno, ne pomeraju stranicu):
- Administracija (tabela korisnika, matrica primalaca obaveštenja) i Audit log na telefonu imaju tabelu sa pomeranjem unutar kartice —
  administratorski ekrani se koriste na računaru;
- sortiranje po kolonama postoji samo u tabelama (od 768 px), na telefonu je redosled po datumu.

Ručno pregledano (snimci ekrana): Početna, ekran uređaja i popunjavanje na 375 px, nacrt šablona i Šabloni na 1366 px, zvono i dijalozi.
