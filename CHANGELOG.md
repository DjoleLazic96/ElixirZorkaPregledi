# Istorija izmena — TPM pregledi

Format po Keep a Changelog, verzije po SemVer (Elixir blueprint 11). Pisano za korisnike aplikacije:
šta vide i šta se promenilo, ne tehnički detalji.

## [Neobjavljeno]

### Novo
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
- **Novi izgled po odobrenom mockup-u**: ElixirZorka logo i zelena linija u zaglavlju, zeleni levi meni sa ikonicama, sadržaj centriran, sva dugmad iste visine i isti razmaci, oznake statusa u boji po značenju (Planiran sivo, U radu žuto, Čeka overu zeleno, Overeno plavo, dorada crveno), stavke lista sa „›“, pretraga kao prozor, Početna sa pozdravom, popunjavanje sa zelenim zaglavljem pregleda i velikim tasterima odgovora.
- Na telefonu su sva dugmad, padajuće liste i izbor datuma dovoljno veliki za prst (44 px), a pregledi uređaja, ispunjenje plana i istorijat šablona su kartice umesto tabela.
- Arhiva pregleda na tabletu prikazuje kartice; zaglavlja i paginacija prelaze u novi red umesto da izlaze iz ekrana.
- Fotografije i slike uputstava se prikazuju i na produkcionom serveru (bezbednosna pravila pregledača).
- Uklonjen primer „Kategorije“ iz skeleta; početni ekran je **Početna**.

### Ispravljeno
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
