# Faza 06 — Overa, dorada, arhiva, izveštaji

**Cilj:** svi vide arhivu i detalj pregleda; Supervizor i Direktor overavaju, vraćaju na doradu, poništavaju i odlučuju o
zahtevima za doradu i spornim unosima; pregled se izvozi u PDF i Word, arhiva u Excel.
**Pravila:** R6.1–R6.10, R5.3–R5.4 (prikaz), R11.1–R11.3, R8.13 (arhiva).

## Odluke

- **Overa** (`ceka_overu` → `overen`): pamti ko, kada i da li je overio Direktor (`overio_direktor`), jer Supervizor ne vraća ni
  ne poništava pregled koji je overio Direktor (R6.5, R6.10).
- **Vrati na doradu** (pre i posle overe): izabrani inženjer (predlaže se isti), razlog obavezan; `dorada` + 1, status
  `vracen_na_doradu`, odgovori prethodne dorade se kopiraju u novu (prethodna ostaje u istoriji). Inženjer menja i predaje
  kao i pre; vreme dorade ne ulazi u aktivno vreme (R6.6).
- **Zahtev za doradu** (Inženjer, svoj predat ili overen pregled): pre overe odlučuje Supervizor/Direktor, posle overe onaj ko je
  overio ili Direktor; odobren = vrati na doradu istom inženjeru.
- **Poništi** (u radu, čeka overu, overen, vraćen): razlog obavezan; pregled ostaje u arhivi kao „Poništen“, broj se ne koristi ponovo.
- **Sporan unos** (iz faze 05): Supervizor/Direktor bira „Primeni“ (odgovor se upisuje u tekuću doradu) ili „Odbaci“.
- **Upozorenja kontroloru** (R5.4) u detalju: odstupanje sata telefona, „Sve neoznačeno je ispravno“ upotrebljeno (broj i sekcije),
  aktivno vreme i broj delova. Pragovi (minimum po tipu) dolaze posle 3–4 nedelje merenja (R5.5).
- **Izveštaji:** PDF (QuestPDF) ceo pregled sa rezimeom problematičnih stavki na početku; Word (OpenXML) isti sadržaj; Excel
  (ClosedXML) arhiva po istim filterima. Vidi se i verzija šablona (R11.1).

## API

`GET /pregledi` (filteri `od`, `do`, `tip`, `status`, `inzenjerId`, `pretraga`, stranice) · `GET /pregledi/{id}` ·
`POST /pregledi/{id}/overi` · `/vrati-na-doradu` `{ inzenjerId, razlog }` · `/zahtev-za-doradu` `{ razlog }` ·
`/zahtev-za-doradu/odluka` `{ odobreno }` · `/ponisti` `{ razlog }` · `/sporan-unos/{unosId}/odluka` `{ primeni }` ·
`GET /pregledi/{id}/izvoz?format=pdf|docx` · `GET /izvoz/pregledi.xlsx`.

## Frontend

`/pregledi` (arhiva: tabela na računaru, kartice na telefonu, filteri, stranice, „Preuzmi Excel“) i `/pregledi/:id` (zaglavlje,
vreme i upozorenja, rezime problematičnih, sekcije sa odgovorima i slikama, van funkcije, istorija, sporni unosi, akcije po pravu,
PDF/Word). Kalendar otvara detail za započete i predate preglede.
