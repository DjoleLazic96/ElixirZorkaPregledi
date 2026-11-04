# 8. Šabloni i uređaji

> **Za koga:** svi (izmene: Direktor i Administrator). **Cilj:** posle čitanja znate gde su šabloni pregleda, kako se traži
> uređaj i kako se registar i katalog menjaju.

## 8.1 Šabloni

Levi meni → **Šabloni**. Gore su dugmad za šest šablona (**DP1**, **DP2**, **PZ1**–**PZ4**) sa brojem verzije, uređaja i stavki.
Izabrani šablon se prikazuje kao papirni obrazac: sekcija po sekcija, uređaj po uređaj, a uz svaku aktivnost dva ponuđena odgovora
(npr. **Okreće / Ne okreće**). Plava oznaka uz aktivnost je **dodatno polje** koje se popunjava uz neispravan odgovor
(npr. „Broj rolni koje ne okreću“, „Temperatura ležaja“).

Kartica **Katalog aktivnosti** prikazuje sve aktivnosti sa parom odgovora i u koliko stavki se koriste. Kartica **Dodatna polja**
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

Izmene samog šablona (dodavanje i izbacivanje stavki, nova verzija) dolaze u kasnijoj verziji.

## 8.4 Uvoz šablona (Administrator)

**Šabloni** → **Uvezi šablone** → izaberite `.xlsx` (list „Stavke“). Uvoz je jednokratan: pravi prvu verziju svakog šablona iz
fajla i popunjava registar i katalog. Ako je bilo koji red neispravan, ništa se ne upisuje, a prozor **Uvoz nije urađen**
nabraja greške sa brojem reda (npr. „Red 397 (PZ1, …): nedostaje naziv uređaja.“). Šablon koji je već uvezen se ne uvozi ponovo.
