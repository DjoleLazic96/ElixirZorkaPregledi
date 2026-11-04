# Faza 07 — Potrebne akcije i kvarovi

**Cilj:** inženjer na stavci otvara potrebnu akciju; na sledećem pregledu sa tim uređajem pop-up pita da li je urađena;
Supervizor/Direktor vode akcije (zaduženi, zatvaranje, otkazivanje); kvarovi se otvaraju i zatvaraju sami iz predatih pregleda.
**Pravila:** R4.7, R7.1–R7.15, R8.1, R8.7, R8.15–R8.16, R11.2 (Excel lista).

## Odluke

- **Akcija** nastaje operacijom `akcija` (`stavkaId`, `vrsta`, `hitnost`, `opis`) kroz `/sinhronizacija` — radi i bez interneta.
  Broj `A-2026-0031` iz brojača. Rok iz hitnosti: Hitno = sutra, 7 dana = +7, sledeći PZ = datum sledećeg planiranog PZ-a, Pratiti = bez roka.
- **Pop-up (R4.7, R7.4):** uz uređaj sa otvorenom akcijom (iz bilo kog pregleda, i PZ) operacija `akcija_odgovor` (`akcijaId`,
  `uradjeno`): da = zatvorena kroz pregled, ne = „ostaje otvorena“ u istoriji. Ako je zatvorena a odgovor na stavci neispravan,
  ekran upozorava i nudi novu akciju (R7.14).
- **Kvar (R8.7, R8.15):** pri predaji: neispravan odgovor otvara kvar `K-2026-0087` za stavku (uređaj + aktivnost) ili mu povećava
  broj prijava; prvi ispravan odgovor ga zatvara; „nije u funkciji“ ne menja ništa. Kad se kvar zatvori, otvorene akcije na
  toj stavci dobijaju oznaku „stavka sada ispravna“ (R7.15). Poništen pregled ne briše kvarove ni akcije.
- Zatvaranje i otkazivanje sa ekrana (R7.5) i zaduženi (R7.12) — pravo `akcije.zatvaranje`, razlog obavezan.

## Tabele (migracija `AkcijeIKvarovi`)
`akcija`, `akcija_dogadjaj`, `kvar`.

## API
`GET /akcije?status&hitnost&sekcijaId&uredjajId&sadaIspravna` · `GET /akcije/{id}` · `POST /akcije/{id}/zatvori|otkazi` `{ razlog }` ·
`PUT /akcije/{id}/zaduzen` `{ zaduzen }` · `GET /kvarovi?otvoreno&sekcijaId&pretraga` · `GET /kvarovi/{id}` · `GET /izvoz/akcije.xlsx` · `GET /izvoz/kvarovi.xlsx`.
