# 4. Raspored, kalendar i odsustva

> **Za koga:** svi (raspored menjaju Supervizor i Direktor). **Cilj:** posle čitanja znate da napravite raspored za nedelju,
> zakažete PZ, prebacite pregled i unesete odsustvo.

## 4.1 Kalendar

Levi meni → **Kalendar**. Na računaru je mesec, na telefonu spisak dana sa pregledima. Boje: bez boje planiran, **žuto** u radu,
**zeleno** završen (čeka overu), **plavo** overen, **crveno** nije započet tog dana. Ako je DP prenet, na starom danu piše
„nije urađen – prenet na …“. Klik na pregled otvara karticu sa brojem, datumom, inženjerom i statusom.

## 4.2 Raspored za nedelju (Supervizor, Direktor)

Levi meni → **Raspored** → izaberite nedelju → **Predloži raspored**. Aplikacija ponudi dane od danas do petka (prošli dani se ne nude),
DP1/DP2 naizmenično i inženjere redom, preskačući neradne dane, dan PZ-a i odsutne. DP-ovi su jedan niz preko nedelja: posle DP1 u
petak, u ponedeljak ide DP2. Inženjera možete promeniti, a dan koji ne želite da zakažete odštiklirajte (**Zakaži**) — tipovi ostalih se
preračunaju. **Potvrdi raspored** — pregledi dobijaju brojeve (npr. DP1-2026-0142).

Ako je dan koji zakazujete usred već zakazanog niza, aplikacija pita: **Umetni i pomeri naredne** (preporučeno — taj dan preuzima sledeći
DP iz rasporeda, naredni idu dan unazad, a kraj nedelje dobija nov DP) ili **Ostavi kako jeste**.

**Podsetnik.** Četvrtkom i petkom posle kraja radnog dana Supervizor i Direktor dobijaju „Raspored za sledeću nedelju“ ako neki dan
sledeće nedelje nema DP (u poruci su ti dani).

**Otkazivanje.** Kartica pregleda (ili stranica pregleda) → **Otkaži pregled** → razlog. Za DP birate:
- **Pomeri raspored** — DP se ne gubi: prelazi na sledeći radni dan, a ceo raspored DP-a se pomera za dan;
- **Otkaži, zadrži raspored** — DP se poništava (ostaje u arhivi, broj se ne koristi ponovo), naredni ostaju gde su.

PZ se samo otkazuje; sledeći PZ po redu se računa bez njega. Inženjer dobija obaveštenje, a otkazan pregled se ne računa u ispunjenje
plana. Započet pregled se ne otkazuje nego poništava (stranica pregleda → **Poništi pregled**).

**Povuci raspored.** Kad posle izmene dan ostane bez DP-a usred niza (npr. PZ je pomeren ili otkazan), aplikacija pita: **Povuci
raspored** (naredni DP-ovi idu dan unazad, redosled i inženjeri po danima ostaju, a poslednji zakazani dan ostaje slobodan za sledeći
predlog) ili **Ostavi prazno**.

**Promeni DP.** Kartica planiranog DP-a → **Promeni DP**: DP1 postaje DP2 (ili obrnuto) i dobija nov broj. Ako posle tog dana ima
zakazanih DP-ova, **Da, uskladi naredne** preokreće i njih da smena ostane ispravna, a **Ne, samo ovaj** menja samo taj dan; ako ih nema,
pitanja nema — samo **Promeni u DP1** (ili DP2).

## 4.3 PZ

**Zakaži PZ**: datum i inženjer; PZ je unapred postavljen na sledeći po redu (PZ1 → PZ4). Izbor drugog je **Promeni PZ** (napomena
nije obavezna), a redosled se nastavlja od zakazanog. Na dan PZ-a DP se ne radi (podešavanje), pa se DP tog dana i naredni pomeraju
za dan napred. Kad izaberete dan sa DP-om, ispod datuma piše koji DP se pomera, a dugme je **Zakaži i pomeri DP-ove**. Isto važi kad se
PZ pomera na drugi dan (**Promeni u rasporedu**) ili kad odobrite raniji početak. Ako je DP tog dana već započet, on ostaje i
taj dan se rade i DP i PZ — to piše ispod datuma. Ako se PZ posle otkaže, prozor **Dan je ostao bez DP-a**
nudi **Povuci raspored**, koje vraća DP-ove kako su bili pre tog PZ-a.

**Odsutni inženjeri.** Inženjer koji je tog dana odsutan ne može da se izabere (poruka navodi dan i vrstu odsustva). Pregled koji je
ostao odsutnom inženjeru ima crvenu oznaku **inženjer odsutan** i stoji u **Čeka moju odluku** dok se ne prebaci. Pregled za koji zahtev
inženjera čeka odluku ima žutu isprekidanu ivicu (**čeka prebacivanje** / **čeka raniji početak**).

**Dva inženjera na PZ-u.** Kalendar → PZ → **Dodaj inženjera**: izaberite drugog inženjera i označite njegove sekcije
(**Označi sve sekcije**, pa skinite one koje ostaju prvom). Inženjer kome je PZ dodeljen radi sve neoznačene sekcije. Svako na svom
telefonu vidi ceo PZ, a upisuje samo u svoje sekcije; predaje bilo ko od njih kad su sve sekcije gotove. Sekcije menjate ili drugog
inženjera uklanjate na istom mestu (**Drugi inženjer**).

## 4.4 Propušten DP

Ako DP do kraja radnog dana (podešavanje) nije ni započet, aplikacija ga sama prenosi na sledeći radni dan, a ceo raspored DP-a
se pomera. Inženjeri ostaju na svojim danima: propušten DP1 sa ponedeljka u utorak radi inženjer raspoređen za utorak. Započet
pregled završava isti inženjer sledećeg radnog dana. Raspored posle toga možete ručno menjati (kartica pregleda → **Promeni u rasporedu**).
U kalendaru propušten pregled na prvobitnom danu ima crvenu oznaku „nije urađen – prenet na …“, a ručno pomeren sivu „pomeren na …“.

## 4.5 Prebacivanje

Inženjer: kalendar → svoj planiran pregled → **Zatraži prebacivanje** (razlog obavezan, predlog zamene nije). Supervizor ili
Direktor: **Raspored** → **Zahtevi inženjera** → **Odluči** → izaberite zamenu → **Odobri** ili **Odbij**. Započet pregled se ne prebacuje.

**Raniji početak PZ-a.** Inženjer može da zatraži da započne PZ pre zakazanog dana (npr. zastoj počinje ranije). Zahtev stiže u
**Čeka moju odluku** i u **Raspored** → **Zahtevi inženjera** (oznaka „raniji početak“). **Odobri** pomera PZ na današnji dan i inženjer
odmah može da ga započne; **Odbij** ostavlja zakazani datum. DP se ne radi ranije — on se radi na svoj dan.

## 4.6 Odsustva

- Inženjer: levi meni → **Moja odsustva** → od, do, vrsta, napomena → **Pošalji zahtev**; ispod je spisak sa statusom.
  Zahtev o kome još nije odlučeno može da se povuče (**Povuci zahtev**).
- Supervizor/Direktor: **Raspored** → **Zahtevi za odsustvo** → **Odluči**, ili **Unesi odsustvo**. Prikazuju se planirani pregledi
  inženjera u tom periodu sa predlogom zamene; **Prebaci sve na** postavlja istu zamenu svima. **Odobri i prebaci** / **Sačuvaj i prebaci**.
- Preklapanje sa postojećim odsustvom nije dozvoljeno: „Za taj period već postoji odsustvo.“
- **Sva odsustva**: **Raspored** → kartica **Odsustva** — tekuća i buduća odsustva svih inženjera; filter po inženjeru, a
  **Prikaži i ranija** dodaje prošla, odbijena i otkazana. **Izmeni** menja period (npr. raniji povratak), **Otkaži** otkazuje odsustvo. Ako su zbog odsustva pregledi bili prebačeni, a sada
padaju na dane kad je inženjer tu, iskoči **Vrati preglede** (zamene i on dobijaju obaveštenje) ili **Ostavi zamenama**.
  Inženjer dobija obaveštenje. Pregledi koji su zbog odsustva prebačeni na zamenu ostaju zameni — vratite ih u rasporedu ako treba.

## 4.7 Neradni dani (Administrator, Direktor)

Levi meni → **Neradni dani** → od, do, naziv, vrsta → **Dodaj**. DP se ne radi neradnim danima; PZ može ako je zakazan. Ako su u tom periodu već
zakazani DP-ovi, pre unosa se vidi njihov spisak: **Unesi i pomeri DP-ove** ih pomera dalje u nizu (inženjeri dobijaju obaveštenje), a
**Odustani** ne unosi ništa. DP koji ipak stoji na neradnom danu (npr. već započet) u kalendaru ima crvenu oznaku „na neradni dan“. Kad se neradni dan
obriše, a taj dan ostane bez DP-a usred niza, nudi se **Povuci raspored**.
