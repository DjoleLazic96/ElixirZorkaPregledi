# Faza 02 — Oprema i šabloni

**Cilj:** u aplikaciji postoje registar opreme, katalog aktivnosti, dodatna polja i šest šablona (verzija 1), uvezeni iz
`docs/sabloni/sabloni-za-uvoz.xlsx`; svi mogu da pregledaju šablone i registar, Direktor i Admin mogu da menjaju registar i katalog.
**Pravila:** R2.1–R2.3, R2.5, R2.7, R2.10–R2.15.
**Uslov:** faza 01 (prava `sabloni.nacrt`, sekcije).

## Ulaz: format uvoza

List „Stavke", jedan red = jedna stavka šablona, redosled reda = redosled u šablonu:

| Kolona | Primer | U bazu |
|---|---|---|
| Šablon | `DP1` | `sablon.tip` |
| Sekcija | `10` | `sekcija.sifra` (mora postojati) |
| Redosled | `1` | `sablon_stavka.redosled` |
| Uređaj – naziv | `Merna vaga Sautelma` | `uredjaj.naziv` |
| Uređaj – oznaka | `10-C-07C` (može biti prazno, npr. `HL1`) | `uredjaj.oznaka` |
| Aktivnost | `Pregled rolni` | `aktivnost.naziv` |
| Odgovor ispravan / neispravan | `Okreće` / `Ne okreće` | `aktivnost.odgovor_*` |
| Dodatno polje | `Broj rolni koje ne okreću` | `sablon_stavka_polje` |
| Ispravka u odnosu na obrazac, Proveriti | tekst | ne uvozi se (samo za ljude) |

List „Izvori" se ne uvozi. Prvi uvoz pravi verziju 1 svakog šablona sa statusom `objavljena` i događaj „Prvi uvoz" u `sablon_dogadjaj`.

## Odluke za ovu fazu

- **Uređaj u registru = par (naziv, oznaka) tačno kako piše u šablonu.** Šabloni DP1, DP2 i PZ1 su prepisani doslovno
  sa obrazaca, pa ista oznaka ponegde ima dva naziva (npr. „Filter 70-F-01" i „Puž filtera 70-F-01"), a oznaka može biti
  prazna. Zato je jedinstven ključ `(naziv, oznaka)`, a ne sama `oznaka` kako stoji u spec-u §6.2; spec se ispravlja
  u istom PR-u. Spajanje dva zapisa istog uređaja radi Direktor/Admin u registru kad odluči (faza 10), istorija ostaje.
- **Naziv uređaja je obavezan.** Na PZ1 jedan red u sekciji 21 nema naziv ni oznaku (tako je na obrascu); uvoz taj red
  odbija dok se u Excelu ne upiše naziv.
- **Aktivnost u katalogu = (naziv, odgovor ispravan, odgovor neispravan).** Ista aktivnost iz više šablona je jedan red kataloga.
- **Vrsta uređaja** se pri uvozu popunjava prvom reči naziva (Transporter, Redler, Ventilator, Puž…), a menja se u registru.
- **Dodatna polja** (seed, ne iz Excela):

  | Naziv | Tip | Jedinica / oznake | Prikaz |
  |---|---|---|---|
  | Broj rolni koje ne okreću | `broj` | kom. | uz neispravan („Ne okreće") |
  | Temperatura ležaja | `dve_vrednosti` | °C; „ležaj do rotora", „ležaj do remenice/spojnice" | uz neispravan („Greje se") |
  | Broj nedostajućih / oštećenih komada | `broj` | kom. | uz neispravan |

  Sva su opciona (R2.11). Naziv polja u Excelu mora da se poklopi sa seed-om, inače uvoz odbija red.
- **Uvoz je samo prvi put:** ako šablon već ima verziju, `POST /sabloni/uvoz` vraća 422 („Šablon DP1 je već uvezen; izmene idu kroz nacrt.").
  Uvoz je jedna transakcija — greška u bilo kom redu ne upisuje ništa i vraća spisak grešaka sa brojem reda.

## Zadaci

### Backend
1. Migracija `OpremaISabloni`: `uredjaj`, `aktivnost`, `dodatno_polje` (+ seed tri polja), `sablon` (+ seed šest tipova),
   `sablon_verzija`, `sablon_stavka` (`kljuc uuid` — stalni ID stavke kroz verzije), `sablon_stavka_polje`, `sablon_dogadjaj`, `fajl`.
   Uklanja se `kategorija` i ceo feature `Kategorije` (backend, frontend, testovi, docs).
2. `Features/Uredjaji`: lista (filter sekcija, vrsta, status, pretraga), detalj (u kojim je šablonima), kreiranje, izmena,
   „van upotrebe" / vrati (R2.3); `qr_token` se generiše pri kreiranju (QR ekran je faza 08).
3. `Features/Aktivnosti`: lista i kreiranje/izmena naziva i para odgovora (R2.7); izmena para odgovora nije dozvoljena ako je
   aktivnost u objavljenoj verziji (422) — to ide kroz novu aktivnost i nacrt.
4. `Features/DodatnaPolja`: lista; dodavanje novog polja (Admin, R2.13).
5. `Features/Sabloni`: `GET /sabloni` (šest šablona, važeća verzija, broj stavki), `GET /sabloni/{tip}/verzije/{broj}`
   (sekcije → uređaji → aktivnosti → polja); `POST /sabloni/uvoz` (Admin, `.xlsx` ≤ 5 MB, ClosedXML).
6. `GET /uredjaji/nije-u-sablonu` (R2.10) — uređaji koji nisu ni u jednoj važećoj verziji.

### Frontend
1. `/sabloni` (sve role čitaju): izbor šablona → sekcije kao trake, uređaji kao kartice sa aktivnostima i parom odgovora,
   oznaka dodatnog polja uz aktivnost. Telefon: jedna sekcija u fokusu.
2. Admin: dugme „Uvezi šablone" (otpremanje `.xlsx`, prikaz grešaka po redu).
3. `/uredjaji` — registar (tabela / kartice na telefonu), filteri, „van upotrebe"; forma za novi uređaj i izmenu (Direktor, Admin).
4. Katalog aktivnosti i dodatna polja kao tabovi u `/sabloni` za Direktora i Admina.

### Testovi
- Unit: parser uvoza (prazna oznaka, nepoznata sekcija, nepoznat par odgovora, nepoznato polje, dupli red, ponovni uvoz → 422);
  ista aktivnost iz dva šablona = jedan red kataloga; isti (naziv, oznaka) u dva šablona = jedan uređaj.
- Integracioni: uvoz pravog `docs/sabloni/sabloni-za-uvoz.xlsx` → 6 verzija, 867 stavki, broj uređaja i aktivnosti kao u fajlu;
  401/403 za sve nove rute; „van upotrebe" se ne nudi pri kreiranju stavke.
- Frontend: prikaz šablona sa dodatnim poljem; forma uređaja; guard za uvoz (samo Admin).

### Docs
`docs/02`, `docs/03`, spec §6.2 (ključ uređaja), `docs/user/07-sabloni.md` (prvi deo: pregled šablona i registra), CHANGELOG.

## Gotovo kada
Admin na čistoj bazi uveze `sabloni-za-uvoz.xlsx`; svaki od šest šablona u aplikaciji izgleda kao njegov papirni obrazac
(isti redosled sekcija, uređaja i aktivnosti, isti odgovori), uz dodatna polja na mestima iz kolone „Dodatno polje";
registar ima sve uređaje; drugi pokušaj uvoza vraća 422.
