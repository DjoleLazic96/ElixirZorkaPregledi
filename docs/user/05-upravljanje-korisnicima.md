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

Levi meni → **Administracija** → kartica **Korisnici**. Tabela: **Korisničko ime**, **Ime**, **E-mail**, **Rola**, **Prava** (broj štikliranih), **Status**, **Poslednja prijava**.
Uključite **Prikaži i neaktivne** da vidite deaktivirane naloge.

## 5.2 Otvaranje naloga

1. Kliknite **Novi korisnik**.
2. Unesite **Korisničko ime** (bez razmaka; ne može se kasnije menjati), **Ime i prezime**, po želji **E-mail**, **Lozinka** (najmanje 8 znakova, cifra, veliko i malo slovo) i izaberite **Rolu**.
3. Kliknite **Kreiraj**. Poruka: „Korisnik je kreiran."
4. Saopštite korisniku ime i lozinku lično; zamolite ga da je odmah promeni (svoje ime → **Promena lozinke**).

## 5.3 Izmena naloga i role

Kliknite **Izmeni** u redu korisnika; menjate **Ime i prezime**, **E-mail** i **Rolu**; **Sačuvaj**. Promena role odjavljuje korisnika sa svih uređaja
i vraća mu podrazumevana prava nove role.

### Prava

Kliknite **Prava** u redu korisnika. Štiklirajte šta korisnik sme (npr. **Overa, dorada i poništavanje pregleda**, **Raspored i PZ**,
**Podešavanja**) i kliknite **Sačuvaj prava**. **Vrati podrazumevana** vraća skup koji ta rola dobija pri otvaranju naloga.
Nova prava važe posle sledeće prijave korisnika, najkasnije za 15 minuta.

**Popunjavanje pregleda** može da ima samo Inženjer — za ostale role je polje zaključano.

## 5.4 Deaktivacija i aktivacija

Korisnici se nikad ne brišu — deaktiviraju se, da bi audit log sačuvao ko je šta radio.
Kliknite **Deaktiviraj**, potvrdite. Deaktiviran korisnik ne može da se prijavi i odjavljen je sa svih uređaja. Vraćanje: **Aktiviraj**.
Ako inženjer ima zakazane preglede, pre deaktivacije se vidi njihov spisak sa predlogom zamene (kao kod odsustva):
**Deaktiviraj i prebaci** ih predaje izabranim inženjerima, a prazna zamena ostavlja pregled da se reši u rasporedu.
Ako neko odlazi iz firme a treba novi nalog istog imena: otvorite novi nalog sa drugim korisničkim imenom.

## 5.5 Postavljanje nove lozinke

Kliknite **Resetuj lozinku** u redu korisnika, unesite **Nova lozinka**, kliknite **Sačuvaj**. Korisnik je odjavljen sa svih uređaja i prijavljuje se novom lozinkom, a aplikacija odmah traži da izabere svoju (tako ni
administrator ne zna lozinku koju korisnik dalje koristi). Saopštite je lično, ne mejlom.

## 5.6 Zaštite koje aplikacija sprovodi

- Ne možete sebi promeniti rolu ni deaktivirati sopstveni nalog.
- Ne možete poslednjem aktivnom administratoru skinuti rolu Administrator ni ga deaktivirati — uvek mora postojati bar jedan.
- Ne možete sebi oduzeti pravo **Korisnici i prava** (inače više ne biste mogli da upravljate nalozima).
- **Popunjavanje pregleda** se ne može dodeliti Supervizoru, Direktoru ni Administratoru.

## 5.9 Sekcije

Administracija → **Sekcije** (samo Administrator): **Nova sekcija** (šifra kao na obrascu, npr. 80; naziv nije obavezan), **Izmeni**
(naziv i **Aktivna**) i strelice za redosled. Sekcija se ne briše: neaktivna se samo ne nudi za nove uređaje, a postojeći uređaji i stari
pregledi ostaju u njoj. Šifra se posle ne menja.

## 5.8 Podešavanja

**Administracija** → kartica **Podešavanja** (pravo „Podešavanja“). Ovde se menjaju vremena upozorenja „DP/PZ nije započet“, kraj
radnog dana (posle njega se nezapočet DP prenosi na sledeći radni dan), da li se DP radi na dan PZ-a, granica pauze, dozvoljeno
odstupanje sata telefona, rok za overu i pragovi za ponovljeni kvar i „dugo van funkcije“. Kliknite **Sačuvaj podešavanja**;
poruka: „Podešavanja su sačuvana.“ Svaka izmena se beleži u audit logu.

## 5.7 Najčešći problemi

### „Ne možete deaktivirati poslednjeg aktivnog administratora."
**Šta da uradite:** prvo dodelite rolu Administrator drugom korisniku, pa ponovite.

### „Korisnik sa korisničkim imenom „…“ već postoji."
**Šta da uradite:** izaberite drugo ime (uključite **Prikaži i neaktivne** — možda postoji deaktiviran nalog koji treba aktivirati).

### „Lozinka mora imati najmanje 8 znakova…"
**Šta da uradite:** lozinka treba da ima cifru, veliko i malo slovo i bar 8 znakova.

### Korisnik kaže da ne može da se prijavi
**Proverite:** da li je nalog aktivan (**Status**), da li je zaključan posle 5 pogrešnih pokušaja (sačekati 10 minuta), da li ima VPN. Po potrebi **Resetuj lozinku**.
