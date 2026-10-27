# 5. Upravljanje korisnicima

> **Za koga:** administrator. **Cilj:** znate da otvorite nalog, promenite rolu, deaktivirate korisnika i postavite novu lozinku.

## Sadržaj

- [5.1 Gde se nalazi](#51-gde-se-nalazi)
- [5.2 Otvaranje naloga](#52-otvaranje-naloga)
- [5.3 Izmena naloga i role](#53-izmena-naloga-i-role)
- [5.4 Deaktivacija i aktivacija](#54-deaktivacija-i-aktivacija)
- [5.5 Postavljanje nove lozinke](#55-postavljanje-nove-lozinke)
- [5.6 Zaštite koje aplikacija sprovodi](#56-zaštite-koje-aplikacija-sprovodi)
- [5.7 Najčešći problemi](#57-najčešći-problemi)

## 5.1 Gde se nalazi

Levi meni → **Administracija** → kartica **Korisnici**. Tabela: **Korisničko ime**, **Ime**, **E-mail**, **Rola**, **Poslednja prijava**, **Status**.
Uključite **Prikaži i neaktivne** da vidite deaktivirane naloge.

## 5.2 Otvaranje naloga

1. Kliknite **Novi korisnik**.
2. Unesite **Korisničko ime** (bez razmaka; ne može se kasnije menjati), **Ime i prezime**, po želji **E-mail**, **Lozinka** (najmanje 8 znakova, cifra, veliko i malo slovo) i izaberite **Rolu**.
3. Kliknite **Kreiraj**. Poruka: „Korisnik je kreiran."
4. Saopštite korisniku ime i lozinku lično; zamolite ga da je odmah promeni (svoje ime → **Promena lozinke**).

## 5.3 Izmena naloga i role

Kliknite **Izmeni** u redu korisnika; menjate **Ime i prezime**, **E-mail** i **Rolu**; **Sačuvaj**. Promena role odjavljuje korisnika sa svih uređaja.

## 5.4 Deaktivacija i aktivacija

Korisnici se nikad ne brišu — deaktiviraju se, da bi audit log sačuvao ko je šta radio.
Kliknite **Deaktiviraj**, potvrdite. Deaktiviran korisnik ne može da se prijavi i odjavljen je sa svih uređaja. Vraćanje: **Aktiviraj**.
Ako neko odlazi iz firme a treba novi nalog istog imena: otvorite novi nalog sa drugim korisničkim imenom.

## 5.5 Postavljanje nove lozinke

Kliknite **Resetuj lozinku** u redu korisnika, unesite **Nova lozinka**, kliknite **Sačuvaj**. Korisnik je odjavljen sa svih uređaja i prijavljuje se novom lozinkom. Saopštite je lično, ne mejlom.

## 5.6 Zaštite koje aplikacija sprovodi

- Ne možete sebi promeniti rolu ni deaktivirati sopstveni nalog.
- Ne možete poslednjem aktivnom administratoru skinuti rolu Administrator ni ga deaktivirati — uvek mora postojati bar jedan.

## 5.7 Najčešći problemi

### „Ne možete deaktivirati poslednjeg aktivnog administratora."
**Šta da uradite:** prvo dodelite rolu Administrator drugom korisniku, pa ponovite.

### „Korisnik sa korisničkim imenom „…“ već postoji."
**Šta da uradite:** izaberite drugo ime (uključite **Prikaži i neaktivne** — možda postoji deaktiviran nalog koji treba aktivirati).

### „Lozinka mora imati najmanje 8 znakova…"
**Šta da uradite:** lozinka treba da ima cifru, veliko i malo slovo i bar 8 znakova.

### Korisnik kaže da ne može da se prijavi
**Proverite:** da li je nalog aktivan (**Status**), da li je zaključan posle 5 pogrešnih pokušaja (sačekati 10 minuta), da li ima VPN. Po potrebi **Resetuj lozinku**.
