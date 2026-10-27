# 1. Prijava i role

> **Za koga:** svi korisnici. **Cilj:** posle čitanja znate da se prijavite i odjavite, promenite lozinku i razumete zašto neke stavke menija ne vidite.

## Sadržaj

- [1.1 Prijava](#11-prijava)
- [1.2 Odjava i trajanje sesije](#12-odjava-i-trajanje-sesije)
- [1.3 Promena lozinke](#13-promena-lozinke)
- [1.4 Šta vidi koja rola](#14-šta-vidi-koja-rola)
- [1.5 O aplikaciji](#15-o-aplikaciji)
- [1.6 Najčešći problemi](#16-najčešći-problemi)

## 1.1 Prijava

1. Otvorite adresu aplikacije u Chrome-u ili Edge-u. Potreban je VPN.
2. Unesite **Korisničko ime** i **Lozinku** koje ste dobili od administratora.
3. Kliknite **Prijavi se**.

Posle prijave otvara se početna stranica. Vaše ime je u gornjem desnom uglu.

> **Napomena:** posle 5 pogrešnih pokušaja nalog se zaključava na 10 minuta. Poruka je ista za pogrešno ime i pogrešnu lozinku: „Neispravno korisničko ime ili lozinka."

## 1.2 Odjava i trajanje sesije

Kliknite na svoje ime u gornjem desnom uglu, pa **Odjavi se**. Ako ne radite u aplikaciji 7 dana, sesija ističe i tražiće se ponovna
prijava sa porukom „Sesija je istekla. Prijavite se ponovo." Dok aktivno radite, sesija se produžava sama.

## 1.3 Promena lozinke

1. Kliknite na svoje ime u gornjem desnom uglu, pa **Promena lozinke**.
2. Unesite **Trenutna lozinka**, **Nova lozinka** i **Potvrda nove lozinke**.
3. Kliknite **Sačuvaj**. Pojavljuje se poruka „Lozinka je promenjena."

Nova lozinka mora imati najmanje 8 znakova, bar jednu cifru, bar jedno veliko i jedno malo slovo. Posle promene, drugi uređaji na kojima ste bili prijavljeni biće odjavljeni.

Ako ste zaboravili lozinku, obratite se administratoru — on može da postavi novu.

## 1.4 Šta vidi koja rola

| Stavka u levom meniju | Korisnik | Administrator |
|---|---|---|
| Kategorije | ✔ (samo pregled) | ✔ (i izmene) |
| Administracija | — | ✔ |
| Audit log | — | ✔ |
| Stanje servera | — | ✔ |

Ako ne vidite stavku, Vaša rola nema pristup toj funkciji.

## 1.5 O aplikaciji

Kliknite na svoje ime, pa **O aplikaciji**. Prikazuje se verzija aplikacije, verzija servera, datum izrade i okruženje. Ako piše
„Učitana je starija verzija. Osvežite stranicu (Ctrl+F5).", pritisnite Ctrl+F5. Verzija je i na dnu levog menija (npr. `v0.1.0`).

## 1.6 Najčešći problemi

### „Neispravno korisničko ime ili lozinka."
**Zašto:** pogrešno ime ili lozinka, zaključan nalog (5 pogrešnih pokušaja) ili deaktiviran nalog.
**Šta da uradite:** proverite tastaturu (Caps Lock, pismo); sačekajte 10 minuta; ako i dalje ne radi, javite administratoru.

### „Sesija je istekla. Prijavite se ponovo."
**Zašto:** niste koristili aplikaciju 7 dana ili je administrator promenio Vaš nalog.
**Šta da uradite:** prijavite se ponovo.

### Stranica se ne otvara / „Ova stranica nije dostupna"
**Zašto:** niste povezani na VPN.
**Šta da uradite:** uključite VPN i osvežite stranicu.

### Ne vidim stavku u meniju
**Zašto:** rola nema pristup. **Šta da uradite:** obratite se administratoru.
