# 05 — Pravila pregleda (sa primerima)

> Pravila iz Priloga A specifikacije onako kako ih kod računa. Kad se pravilo menja, menja se i ovaj dokument i test koji ga pokriva.
> Kod: `Domain/Entities/AktivnoVreme` (u `Popunjavanje.cs`), `RasporedPravila.cs`, `AnalitikaPravila.cs`, `AkcijaPravila.cs`;
> `Features/Popunjavanje` (kvarovi), `Features/Analitika` (ispunjenje plana).

## 1. Aktivno vreme pregleda (R5.2, R5.3, R6.6)

Zbir razmaka između uzastopnih operacija sa telefona (vreme sa telefona), uključujući predaju, ali samo razmaka koji nisu veći od
**granice pauze** (podešavanje, podrazumevano 10 min). Veći razmak se ne računa i deli pregled na novi **deo**. Vreme dorade se ne
računa (aktivno vreme se računa samo pri prvoj predaji).

| Operacija | Vreme | Razmak | Računa se |
|---|---|---|---|
| Započni | 07:30 | — | — |
| odgovor | 07:34 | 4 min | da |
| odgovor | 07:41 | 7 min | da |
| odgovor | 08:05 | 24 min | **ne** → počinje 2. deo |
| odgovor | 08:12 | 7 min | da |
| Predaja | 08:15 | 3 min | da |

Aktivno vreme = 4 + 7 + 7 + 3 = **21 min**, **2 dela** (u detalju pregleda: „Pregled je rađen u 2 dela.“).

## 2. Kvar (R8.1, R8.7, R8.15)

Računa se pri **predaji**, po stavci (uređaj + aktivnost):
- neispravan odgovor → otvara kvar `K-2026-0087` (dan = planirani dan pregleda) ili, ako je već otvoren, dodaje prijavu
  (dorada istog pregleda nije nova prijava);
- ispravan odgovor → zatvara otvoren kvar (dan zatvaranja = planirani dan tog pregleda) i otvorenim akcijama na stavci daje oznaku
  „stavka sada ispravna“;
- „nije u funkciji“ ne menja ništa.

Primer — Transporter 10-C-08, „Pregled rolni“: 9.11. neispravno (otvara K-2026-0001), 10.11. neispravno (2. prijava),
11.11. nije u funkciji (bez promene), 12.11. ispravno (zatvara). Trajanje = 12.11 − 9.11 = **3 dana**, 2 prijave.

**Ponovljeni kvar (R8.2):** pri predaji, za svaku neispravnu stavku broje se predati (neponišteni) pregledi u poslednjih M dana
(računajući ovaj) u kojima je ista stavka neispravna; ≥ N → obaveštenje. Podrazumevano N = 3, M = 30: prijave 9.11, 10.11 i 1.12
→ 1.12 je treća u periodu 2.11–1.12 → obaveštenje.

## 3. Istorija i analitika uređaja (R8.6, R8.7)

Stanje uređaja na predatom pregledu: „nije u funkciji“ ako je tako označen; „neispravan“ ako je bar jedna njegova stavka neispravna;
inače „ispravan“. **Između dva pregleda važi stanje sa poslednjeg pregleda.** Kvar je niz dana „neispravan“; dani „nije u funkciji“
ga ne prekidaju i ne produžavaju. Poništeni pregledi se ne računaju.

| Dan | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Stanje | I | I | I | N | N | V | N | I | I | N |

Kvarovi: dani 4, 5, 7 → trajanje **3** (dan 6 se preskače); dan 10 → **1** (otvoren). Broj kvarova 2, prosek 2 dana, najduže bez kvara
3 dana, trenutno bez kvara 0, od kraja poslednjeg kvara (dan 8) do danas (dan 10) **2 dana**.

Status na ekranu uređaja: „nije pregledan“ ako nema predatog pregleda ili je poslednji stariji od 7 dana; inače „nije u funkciji“,
„otvoren kvar“ ili „ispravan“.

## 4. Pomeranje rasporeda — prenos propuštenog DP-a (R3.2–R3.5)

Posle **kraja radnog dana** (podešavanje, 15:00) današnji DP koji nije predat ide na sledeće mesto; **inženjeri ostaju na svojim danima**,
pa svaki kasniji planirani DP ide jedno mesto napred i preuzima inženjera tog dana. Poslednji dobija sledeći dan za DP (radni dan koji
nije neradni ni dan PZ-a, osim ako je uključeno „DP se radi i na dan PZ-a“) i inženjera po rotaciji. Započet pregled zadržava svog inženjera.

| | Pon 9.11 | Uto 10.11 | Sre 11.11 | Čet 12.11 |
|---|---|---|---|---|
| Pre (Ana, Bora, Ceca) | DP1-0001 Ana | DP2-0001 Bora | DP1-0002 Ceca | — |
| Posle prenosa 9.11. u 15:00 | — | DP1-0001 Bora | DP2-0001 Ceca | DP1-0002 Ana (po rotaciji) |

Redosled DP1/DP2 ostaje, broj pregleda se ne menja, a u istoriji svakog pregleda stoji „Prenet na sledeći radni dan“ (stari i novi dan i
inženjer). Ispunjenje plana računa preneti pregled onome kome je bio dodeljen pre prvog prenosa.

## 5. Ispunjenje plana (R8.9)

Pregledi po **prvobitnom** danu, zaključno sa jučerašnjim (današnji još traje), bez poništenih:
- **na dan** — predat istog (beogradskog) dana ili ranije; **kasnije** — predat posle; **propušteno** — nije predat; **preneto** — planirani dan
  različit od prvobitnog;
- **overeno u roku** — sati radnih dana od predaje do overe ≤ „Rok za overu“ (24 h); vikend se ne broji;
- po inženjeru se ne računaju pregledi na dan njegovog odobrenog odsustva (ulaze u zbir i u mesec).

Primer (novembar, danas 14.11): 9.11 predat 9.11 i overen posle 21 h; 10.11 prenet i predat 11.11; 12.11 predat 12.11, neoveren;
13.11 nije predat (inženjer bio na odsustvu). Ukupno: planirano 4, na dan 2 (**50 %**), kasnije 1, propušteno 1, preneto 1; overeno u roku
1 od 3 predata (**33,3 %**), prosečno 21 h do overe. Red inženjera: planirano 3.

**Rok overe — primer:** predat u petak u 12:00, rok 24 h → ističe u ponedeljak u 12:00 (subota i nedelja se ne broje); u ponedeljak u
13:00 je 25 h → podsetnik primaocima, jednom po doradi.

## 6. Rok akcije (R7.2)

Iz hitnosti, od dana kad je akcija otvorena: **Hitno** = sutra, **U narednih 7 dana** = +7 dana, **U sledećem planskom zastoju** = dan
sledećeg planiranog PZ-a, **Pratiti** = bez roka. Akcija otvorena 9.11. kao „U narednih 7 dana“ ima rok 16.11; od 17.11. je crvena i
primaoci dobijaju jedno obaveštenje „Istekao rok akcije“.
