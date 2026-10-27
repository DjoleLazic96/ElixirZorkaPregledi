# 2. Kategorije

> **Za koga:** svi (pregled); administrator (izmene). **Cilj:** znate da pronađete, dodate, izmenite i deaktivirate kategoriju.
>
> *Ovo je uputstvo za PRIMER šifarnika iz skeleta. Kad napravite svoj prvi šifarnik, kopirajte ovaj dokument, preimenujte ga i zamenite nazive.*

## Sadržaj

- [2.1 Gde se nalazi](#21-gde-se-nalazi)
- [2.2 Pretraga i sortiranje](#22-pretraga-i-sortiranje)
- [2.3 Dodavanje kategorije](#23-dodavanje-kategorije)
- [2.4 Izmena kategorije](#24-izmena-kategorije)
- [2.5 Deaktivacija i vraćanje](#25-deaktivacija-i-vraćanje)
- [2.6 Najčešći problemi](#26-najčešći-problemi)

## 2.1 Gde se nalazi

U levom meniju kliknite **Kategorije**. Vidite tabelu sa kolonama **Šifra**, **Naziv**, **Opis** i, za administratora, akcijama u poslednjoj koloni.

## 2.2 Pretraga i sortiranje

- U polje **Pretraga** ukucajte deo šifre ili naziva — lista se filtrira dok kucate. Pretraga ne pravi razliku između č/ć/c.
- Kliknite na zaglavlje **Šifra** ili **Naziv** da sortirate; ponovni klik menja smer.
- Ispod tabele je navigacija po stranicama („Prikaz 1–50 od 123") i izbor broja redova po strani.
- Uključite **Prikaži i neaktivne** da vidite i deaktivirane kategorije (prikazane sivo, sa oznakom **Neaktivna**).

## 2.3 Dodavanje kategorije *(administrator)*

1. Kliknite **Nova kategorija** u gornjem desnom uglu.
2. Unesite **Šifra** (obavezno, do 32 znaka) i **Naziv** (obavezno, do 200 znakova). **Opis** nije obavezan.
3. Kliknite **Sačuvaj**. Pojavljuje se poruka „Kategorija je kreirana." i red u tabeli.

> **Napomena:** šifra mora biti jedinstvena među aktivnim kategorijama. Deaktivirana kategorija može da ima istu šifru kao nova.

## 2.4 Izmena kategorije *(administrator)*

Kliknite **Izmeni** u redu kategorije, izmenite polja, kliknite **Sačuvaj**. Poruka: „Kategorija je sačuvana."

## 2.5 Deaktivacija i vraćanje *(administrator)*

Kategorije se ne brišu nego deaktiviraju — tako istorija ostaje netaknuta.

1. Kliknite **Deaktiviraj** u redu kategorije.
2. Potvrdite u prozoru **Deaktivacija kategorije** klikom na **Deaktiviraj**.
3. Kategorija nestaje iz liste (osim ako je uključeno **Prikaži i neaktivne**). Vraća se dugmetom **Aktiviraj**.

## 2.6 Najčešći problemi

### „Šifra je obavezna." / „Naziv je obavezan."
**Zašto:** polje je prazno. **Šta da uradite:** popunite polje i kliknite **Sačuvaj**.

### „Kategorija sa ovom šifrom već postoji."
**Zašto:** aktivna kategorija sa tom šifrom već postoji.
**Šta da uradite:** izaberite drugu šifru, ili uključite **Prikaži i neaktivne** da proverite da li postoji deaktivirana koju treba vratiti.

### Ne vidim dugme **Nova kategorija** ni akcije u redovima
**Zašto:** Vaša rola je Korisnik — vidite samo pregled. **Šta da uradite:** izmene traži od administratora.

### „Nema kategorija za zadate filtere."
**Zašto:** pretraga ili filter ne daju rezultat. **Šta da uradite:** obrišite polje **Pretraga** ili uključite **Prikaži i neaktivne**.
