# 8. Šabloni i uređaji

> **Za koga:** svi (izmene: Direktor i Administrator). **Cilj:** posle čitanja znate gde su šabloni pregleda, kako se traži
> uređaj i kako se registar i katalog menjaju.

## 8.1 Šabloni

Levi meni → **Šabloni**. Gore su dugmad za šest šablona (**DP1**, **DP2**, **PZ1**–**PZ4**) sa brojem verzije, uređaja i stavki.
Izabrani šablon se prikazuje kao papirni obrazac: sekcija po sekcija, uređaj po uređaj, a uz svaku aktivnost dva ponuđena odgovora
(npr. **Okreće / Ne okreće**). Plava oznaka uz aktivnost je **dodatno polje** koje se popunjava uz neispravan odgovor
(npr. „Broj rolni koje ne okreću“, „Temperatura ležaja“).

Ikonica **?** uz aktivnost otvara **uputstvo** (tekst i do 3 slike); ako ga nema, piše „Uputstvo još nije uneto“. Ista ikonica je i u
popunjavanju pregleda, gde tekst radi i bez interneta. Iznad šablona je izbor **Verzija** za pregled starijih verzija.

Kartica **Katalog aktivnosti** prikazuje sve aktivnosti sa parom odgovora i u koliko stavki se koriste; oznaka „bez uputstva“ pokazuje gde ga nema. Kartica **Dodatna polja**
prikazuje polja i kada se prikazuju.

## 8.2 Uređaji

Levi meni → **Uređaji**. Svaki uređaj postoji jednom; šabloni pokazuju na njega. Uz uređaj piše sekcija, vrsta i u kojim je
šablonima. Pretraga radi po nazivu i oznaci; filteri su **Sekcija**, **Status** i **Nije ni u jednom pregledu**.

## 8.3 Izmene (pravo „Registar opreme i nacrt šablona“)

- **Novi uređaj**: naziv, oznaka (prazno ako je nema), sekcija, vrsta. Isti naziv i oznaka ne mogu postojati dva puta.
- **Izmeni**: ispravka naziva, oznake, sekcije ili vrste; istorija ostaje vezana za uređaj.
- **Van upotrebe**: uređaj se ne briše; ostaje u istoriji, ali se ne nudi za nove šablone. **Vrati u upotrebu** ga vraća.
- **Nova aktivnost / Izmeni** u katalogu: naziv i dva odgovora. Odgovori aktivnosti koja je u važećem šablonu se ne menjaju
  (promenilo bi značenje već datih odgovora) — tada se pravi nova aktivnost i menja kroz nacrt šablona.

- **Uputstvo**: u prozoru uputstva (**?**) → **Unesi uputstvo** / **Izmeni uputstvo** i **Dodaj sliku** (najviše 3). Važi u svim šablonima
  i ne pravi novu verziju.

## 8.4 Uvoz šablona (Administrator)

**Šabloni** → **Uvezi šablone** → izaberite `.xlsx` (list „Stavke“). Uvoz je jednokratan: pravi prvu verziju svakog šablona iz
fajla i popunjava registar i katalog. Ako je bilo koji red neispravan, ništa se ne upisuje, a prozor **Uvoz nije urađen**
nabraja greške sa brojem reda (npr. „Red 12 (DP1, Elevator 10-C-13): nepoznata sekcija „99“.“). Šablon koji je već uvezen se ne uvozi ponovo.

## 8.5 Nova verzija šablona (nacrt i objava)

1. **Šabloni** → izaberite šablon → **Nacrt nove verzije** → **Otvori nacrt**. Nacrt je kopija poslednje verzije; pregledi i dalje
   rade po važećoj.
2. U nacrtu: **Dodaj stavku** (sekcija, uređaj iz registra, aktivnost iz kataloga, dodatna polja), a uz svaku stavku strelice
   **gore / dole**, **izmeni** (druga aktivnost iz kataloga ili polja) i **izbaci**. Novi uređaj ili nova aktivnost se prvo dodaju u
   registar ili katalog.
3. Gore su **Razlike** prema važećoj verziji (Dodato, Izbačeno, Izmenjeno, Pomereno) i **Odobreni predlozi** koje treba sprovesti.
4. **Objavi** (samo Direktor) → **Važi od** (danas ili kasnije). Ako je danas, verzija odmah važi; ako je kasnije, „zakazana“ je i počinje
   da važi tog dana. Pregledi započeti ranije završavaju se po staroj verziji. **Odbaci nacrt** briše nacrt bez promene šablona.

Izbačen uređaj ostaje u registru i u istoriji; ako nije ni u jednom šablonu, vidi se uz filter **Nije ni u jednom pregledu** (Uređaji).

## 8.6 Predlozi izmena

Svako može da predloži izmenu: **Šabloni** → **Predloži izmenu** → vrsta (dodavanje, izbacivanje, ispravka teksta), stavka, šta treba
promeniti i **razlog** (obavezan). Direktor i Administrator predlog vide u zvonu i na Početnoj, a na kartici **Predlozi izmena**
ga **odobre** ili **odbiju** (uz obrazloženje). Podnosilac dobija obaveštenje. Odobren predlog se pojavljuje u nacrtu, a posle objave
piše „u verziji N“.

## 8.7 Istorijat šablona

Kartica **Istorijat**: svi događaji sa šablonima (prvi uvoz, nacrt i izmene u njemu, predlozi i odluke, objave, početak važenja verzije),
sa vremenom, šablonom, verzijom, opisom i ko je to uradio. Filteri: šablon, vrsta događaja, period.
