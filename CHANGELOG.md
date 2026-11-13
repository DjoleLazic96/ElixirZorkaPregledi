# Istorija izmena — TPM pregledi

Format po Keep a Changelog, verzije po SemVer (Elixir blueprint 11). Pisano za korisnike aplikacije:
šta vide i šta se promenilo, ne tehnički detalji.

## [Neobjavljeno]

### Novo
- Neradni dan na dan sa zakazanim DP-om: pre unosa spisak DP-ova i „Unesi i pomeri DP-ove“ (DP-ovi idu dalje u nizu); DP koji ipak stoji na neradnom danu je u kalendaru crveno označen.
- Zakazivanje ili pomeranje PZ-a na dan kad je DP već započet: upozorenje da DP ostaje i da se taj dan rade oba.
- Zatvaranje akcije van pregleda: inženjer šalje „Zahtev za zatvaranje“ (šta je urađeno, kada, ko, po želji slika; radi i bez interneta), Supervizor ili Direktor odobrava ili odbija. I zatvaranje sa ekrana sada traži šta, kada i ko; to se vidi na akciji, u istoriji i u Excelu.
- Detalj pregleda: „Rezime“ (otvara se prvi) i „Ceo pregled“ sa sekcijama kao dugmadima. Rezime pokazuje i akcije otvorene na pregledu, odgovore na ranije akcije, napomene uz ispravne stavke, „Sve ispravno“ i predloge izmene šablona.
- PDF i Word: neispravno crveno, akcija plavo ispod stavke, van funkcije narandžasto; na kraju lista akcija sa pregleda.
- Inženjer može da otkaže svoj zahtev za doradu, prebacivanje, raniji početak ili zatvaranje akcije dok čeka odluku.
- Filteri na telefonu skupljeni iza dugmeta „Filteri“, izabrane vrednosti kao oznake sa × (8 ekrana sa listama).
- Upozorenje kad se PZ zakazuje ili pomera na dan sa DP-om: taj DP i naredni idu dan napred.
- Otkazivanje planiranog pregleda uz razlog; za DP „Pomeri raspored“ ili „Otkaži, zadrži raspored“. Otkazan pregled se ne računa u ispunjenje plana.
- „Povuci raspored“ kad dan ostane bez DP-a usred niza (npr. pomeren ili otkazan PZ).
- Predlog rasporeda: kvačica „Zakaži“ uz svaki dan i „Umetni i pomeri naredne“ za dan usred niza.
- „Promeni DP“ (DP1 ↔ DP2) uz usklađivanje narednih.
- Rezime pre predaje pregleda; „Pogledaj pregled“ kod predatog; u arhivi inženjer vidi svoje preglede.
- „Nešto fali u šablonu? Predloži izmenu“ tokom popunjavanja (i bez interneta).
- Predlozi: „Samo moji“, „Slažem se sa predlogom“, zatvaranje kao duplikat.
- U nacrtu: „+ Nova aktivnost“ i „+ Novi uređaj“ iz prozora „Dodaj stavku“.
- Sekcije u Administraciji (dodavanje, naziv, redosled, neaktivna).
- „Zaboravili ste lozinku?“ na prijavi; obavezna promena lozinke posle reseta.
- „Dostupna je nova verzija“ sa dugmetom „Osveži“.
- Slika koju server ne primi vidi se na svakom ekranu inženjera sa razlogom: „Pošalji ponovo“ (dok se pregled popunjava), „Sačuvaj na telefon“ ili „Odbaci“.
- Testovi kroz pravi pregledač (Playwright) u CI-ju: prijava, meni po rolama i ceo tok inženjera bez interneta.
- Popunjavanje: sličice dodatih slika uz stavku; dodir otvara sliku, a iz nje se briše (i bez interneta).
- Popunjavanje: dugmad za prethodnu i sledeću sekciju na dnu sekcije; „Sledeća“ je zelena kad je sekcija gotova.
- Stranica pregleda: „Otključaj za drugi uređaj“ (Supervizor, Direktor) kad je telefon zamenjen ili je pregled započet u pogrešnom browseru.
- Pregled koji je ostao odsutnom inženjeru je crveno označen u Rasporedu, Kalendaru i na Početnoj i stoji u „Čeka moju odluku“.
- Pregled za koji zahtev inženjera čeka odluku ima žutu isprekidanu oznaku u Kalendaru i Rasporedu.
- **Odsustva**: na Rasporedu spisak svih odsustava (filter po inženjeru, i ranija), sa izmenom perioda i otkazivanjem; inženjer povlači svoj zahtev dok čeka odluku.
- Na stranici pregleda Supervizor i Direktor imaju „Promeni u rasporedu“, a za PZ i „Dodaj inženjera“ (kao u Kalendaru).
- **Dva inženjera na PZ-u**: Supervizor ili Direktor u Kalendaru dodaje drugog inženjera i deli sekcije („Označi sve sekcije“).
- Istorijat šablona ima i filter po korisniku.
- Pregled se započinje najranije na planirani dan. Za PZ inženjer može da zatraži raniji početak (uz razlog); Supervizor ili Direktor odobrava, i PZ se pomera na danas.
- Posle prijave aplikacija otvara stranicu sa koje ste došli (npr. QR nalepnica uređaja), a ne uvek Početnu.
- **Nova verzija šablona u aplikaciji**: nacrt (dodavanje, izbacivanje, izmena i redosled stavki), pregled razlika i objava sa datumom od kog važi; započeti pregledi se završavaju po staroj verziji.
- **Predlozi izmena šablona** uz obavezan razlog; Direktor i Administrator ih odobravaju ili odbijaju.
- **Istorijat šablona** sa filterima i izbor starije verzije za pregled.
- **Uputstva za aktivnosti** (tekst i do 3 slike) iza ikonice „?“, i u popunjavanju bez interneta.
- **Obaveštenja**: zvono u zaglavlju (sa brojem stvari koje čekaju Vašu odluku), obaveštenja na telefonu i mejlom.
- Inženjer dobija obaveštenje o dodeli i promeni dodele, prenosu, vraćanju na doradu i ishodu svojih zahteva.
- Automatske provere: pregled nije započet do 13h, podsetnik za overu, istekao rok akcije, hitna akcija, ponovljeni kvar, uređaj dugo van funkcije, dnevni rezime i podsetnik za raspored sledeće nedelje.
- Administrator u Podešavanjima bira ko prima koje obaveštenje.
- **Početna**: današnji i sutrašnji pregledi, overe, akcije po hitnosti, kvarovi, uređaji van funkcije i ispunjenje plana; inženjer ima „Moje aktivnosti“, Supervizor i Direktor „Čeka moju odluku“.
- **Ekran uređaja**: status, istorija u bojama sa klizačem perioda, dani bez kvara i trajanje kvarova, svi pregledi uređaja i dosije (napomene, slike, akcije), QR nalepnica za štampu.
- **Analitika**: ispunjenje plana po mesecu i inženjeru, kvarovi po sekciji, uređaju i aktivnosti, trajanje pregleda.
- **Pretraga** u zaglavlju po broju pregleda, akcije ili kvara i nazivu uređaja, sa žutim obeležavanjem.
- **Potrebne akcije**: akcija na stavci pregleda (vrsta, hitnost, rok), pop-up „da li je urađena“ na sledećem pregledu, lista sa filterima, zaduženi, zatvaranje i otkazivanje, izvoz u Excel.
- **Neispravnosti**: kvarovi se sami otvaraju i zatvaraju iz predatih pregleda, sa trajanjem, svim prijavama i akcijama na stavci.
- **Pregledi**: arhiva sa filterima i izvozom u Excel, detalj pregleda sa aktivnim vremenom, upozorenjima, problematičnim stavkama, slikama i istorijom; PDF i Word izveštaj.
- Overa, vraćanje na doradu, zahtev za doradu, poništavanje i odluka o spornim unosima.
- **Rad bez interneta**: aplikacija se dodaje na početni ekran telefona, pregledi za danas i narednih 7 dana su na telefonu, a urađeno se samo šalje kad se veza vrati („Predato – čeka slanje“, traka „Bez interneta“, „Poslednja sinhronizacija“).
- Prijava: **Zapamti me na ovom uređaju** i dugme „oko“ uz lozinku.
- **Popunjavanje pregleda** na telefonu: odgovori, napomene, dodatna polja, slike, „Nije u funkciji“, „Sve neoznačeno u sekciji je ispravno“ i predaja; aplikacija meri aktivno vreme.
- Na telefonu je meni na dnu ekrana.
- **Kalendar** (mesec na računaru, spisak na telefonu) i **Raspored**: predlog DP-a za nedelju po rotaciji, potvrda sa brojevima pregleda, zakazivanje PZ-a, zahtevi za prebacivanje i odsustva sa prebacivanjem.
- Propušten DP se posle kraja radnog dana sam prenosi na sledeći radni dan; inženjeri ostaju na svojim danima.
- **Moja odsustva** za inženjere i **Neradni dani** za administratora i direktora.
- **Šabloni**: šest šablona pregleda (DP1, DP2, PZ1–PZ4) prikazani kao papirni obrasci, katalog aktivnosti i dodatna polja; administrator ih uvozi iz Excela.
- **Uređaji**: registar opreme sa pretragom, filterima i statusom „van upotrebe“.
- Četiri role: Inženjer, Supervizor, Direktor i Administrator; administrator svakom korisniku štiklira prava (**Administracija → Korisnici → Prava**).
- **Administracija → Podešavanja**: vremena upozorenja, kraj radnog dana, granica pauze, rok za overu i pragovi obaveštenja.

### Izmenjeno
- „O aplikaciji“: tehnički podaci samo za Administratora.
- Podsetnik za raspored stiže četvrtkom i petkom i navodi dane sledeće nedelje bez DP-a.
- Ručno pomeren pregled ima sivu oznaku „pomeren na“; crveno „nije urađen“ samo za propušten.
- Ekrani se učitavaju tek kad zatrebaju: prvi paket aplikacije je oko 40 % manji (rad bez interneta ostaje isti).
- **Novi izgled po odobrenom mockup-u**: ElixirZorka logo i zelena linija u zaglavlju, zeleni levi meni sa ikonicama, sadržaj centriran, sva dugmad iste visine i isti razmaci, oznake statusa u boji po značenju (Planiran sivo, U radu žuto, Čeka overu zeleno, Overeno plavo, dorada crveno), stavke lista sa „›“, pretraga kao prozor, Početna sa pozdravom, popunjavanje sa zelenim zaglavljem pregleda i velikim tasterima odgovora.
- Na telefonu su sva dugmad, padajuće liste i izbor datuma dovoljno veliki za prst (44 px), a pregledi uređaja, ispunjenje plana i istorijat šablona su kartice umesto tabela.
- Arhiva pregleda na tabletu prikazuje kartice; zaglavlja i paginacija prelaze u novi red umesto da izlaze iz ekrana.
- Fotografije i slike uputstava se prikazuju i na produkcionom serveru (bezbednosna pravila pregledača).
- Uklonjen primer „Kategorije“ iz skeleta; početni ekran je **Početna**.

### Ispravljeno
- Predugačka napomena ili opis akcije više ne blokira slanje sa telefona: polja imaju ograničenje i brojač, a server odbija samo tu promenu.
- „Poništi pregled“ šalje obaveštenje inženjeru i onome ko je overio, i zatvara zahteve koji su čekali odluku.
- Poništen pregled se ne računa za „Prošli put nije bio u funkciji“.
- Pomeranje PZ-a na dan sa DP-om (i odobren raniji početak) pomera DP-ove kao i zakazivanje.
- Pitanje „Da li je akcija urađena?“ više ne iskače na pregledu na kom je akcija otvorena, niti se vraća posle odgovora „Ne“.
- „Promeni DP“ ne pita za naredne DP-ove kad ih nema.
- Neradni dani nude godine od 2026; datum u prozoru za odsustvo se više ne seče; poravnata kvačica u filterima; oznaka „stavka sada ispravna“ svuda zelena.
- Kad server ili baza nisu dostupni, aplikacija piše „Server trenutno nije dostupan. Pokušajte ponovo za minut.“ umesto tehničke poruke na engleskom.
- „Predloži raspored“ više ne nudi dane koji su prošli.
- Predlog rasporeda računa DP1/DP2 od DP-a dan pre (nisu mogla da nastanu dva ista zaredom).
- Greška pri prijavi se najavljuje čitaču ekrana.
- Dorada ili kasna predaja starijeg pregleda više ne otvara ni ne zatvara kvar koji je noviji pregled već odredio.
- Rok i broj akcije otvorene posle ponoći računaju se po beogradskom danu.
- Obavezna dodatna polja se proveravaju pri predaji (i na telefonu, bez interneta).
- Sporan unos nastaje samo za inženjera koji je bio na tom pregledu; ostalima se izmena samo odbija.
- Brisanje slike briše i fajl; u doradi se brišu samo slike dodate u toj doradi.
- Slanje slike proverava sekciju (PZ sa dva inženjera) i stvarni format fajla (JPG, PNG, WebP).
- Pregled ne može da se zakaže ni pomeri na dan koji je prošao.
- Neispravan zahtev za uvoz šablona (bez fajla) vraća 400 umesto 500.
- Trajanje kvara i „van funkcije“ se ne prikazuje kao negativan broj dana.
- Posle predaje pregleda Kalendar i Početna odmah pokazuju novi status.
- Posle odjave sledeći korisnik ne dobija ekran na kom je prethodni stao.
- Vreme početka i predaje pregleda se ne ispisuje sa duplim datumom.
- Aktivno vreme u Word/PDF izveštaju se zaokružuje isto kao na ekranu.
- Datum na Početnoj: naziv meseca malim slovom („9. novembar“).
- Početna: ispod pozdrava stoji samo datum (uklonjena napomena prepisana iz mockup-a).
- PDF i Word na stranici pregleda ne mogu da se kliknu dok pregled ne počne (izveštaj bi bio prazan).
- Istorija pregleda pokazuje šta je promenjeno u rasporedu (inženjer, datum), raniji početak i sekcije drugog inženjera.
- Kad server odbije promene sa telefona, telefon to odmah jasno javlja (sa razlogom) i vraća pregled na stanje sa servera; više nema lažnog „Predato – čeka slanje“.
- Odsutan inženjer ne može da se izabere ručno (Zakaži PZ, Promeni u rasporedu, drugi inženjer na PZ-u, zamena, potvrda predloga).
- Dok zahtev za prebacivanje ili raniji početak čeka odluku, umesto dugmeta piše „Zahtev poslat — čeka odluku“.
- „Zatraži prebacivanje“ sa Početne otvara Kalendar na tom pregledu.
- Broj slika je u drugom redu dugmeta „Fotografiši“ (tekst više ne izlazi iz dugmeta na telefonu).
- Uklonjene suvišne napomene na prijavi i u prozorima za izmenu i otkazivanje odsustva.
- Na telefonu su i dugmad pregleda u nedeljnom rasporedu i dugme „Slika“ najmanje 44 px.
- Bez interneta odgovori, slike i predaja se odmah čuvaju na telefonu (ranije su čekali povratak veze).
- Slike snimljene bez interneta se više ne gube kad se pregled preda: telefon prvo pošalje slike, pa tek onda predaju. Slika koju server odbije ostaje sačuvana na telefonu, i posle odjave.
- Jedna neispravna promena sa telefona više ne zaustavlja slanje svih ostalih: odbija se samo ta promena, uz razlog.
- Slike do 3 MB i uvoz šablona do 5 MB više ne odbija web server (greška 413).
- Sporan odgovor se primenjuje samo na pregled koji je u radu ili na doradi; predat ili overen pregled prvo se vraća na doradu.
- Kad dvoje istovremeno odluče o istom pregledu (npr. overa i vraćanje na doradu), druga odluka dobija poruku da je pregled u međuvremenu izmenjen, umesto da tiho prepiše prvu.

### Bezbednost
- Kad više inženjera koristi isti telefon, svako šalje samo svoje neposlate promene; posle isteka sesije sledeći prijavljeni ne šalje tuđe urađeno pod svojim imenom.
- Ažurirani `follow-redirects` (1.16.3) i `undici` (7.30.2) zbog bezbednosnih nalaza visoke ozbiljnosti (`npm audit`).
- 

### Napomene za nadogradnju
- Postojeći nalozi sa rolom „Korisnik“ postaju „Inženjer“; nalozi bez prava dobijaju podrazumevana prava svoje role pri prvom startu.
- Migracija `PregledVerzija` ne menja šemu (koristi sistemsku kolonu `xmin`), ali je potrebna da bi se istovremene izmene pregleda prepoznale.
- nginx u frontend slici sada prima zahteve do 6 MB (`client_max_body_size 6m`); potreban je novi build frontend slike.
- Neposlate promene sa telefona iz prethodne verzije pripisuju se prvom inženjeru koji se prijavi posle nadogradnje.
- 

## [0.1.0] — 2026-10-12

### Novo
- Početni tpm-pregledi: prijava, korisnici i role, promena lozinke, audit log, stanje servera, verzija aplikacije
  („O aplikaciji"), primer šifarnika „Kategorije".
