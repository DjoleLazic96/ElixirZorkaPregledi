# TODO — TPM pregledi

> Radni dnevnik projekta. **Status je ovde**, ne u planovima. Oznake: `[ ]` otvoreno, `[~]` u toku, `[x]` gotovo.
> Odluke se zapisuju u kontekstu kao **ODLUKA** i prepisuju u `docs/odluke.md`.

## Otvoreno

### Pre početka razvoja
- [ ] Vlasnik projekta (Miroslav Zec) odobrava spec `docs/superpowers/specs/2026-10-27-tpm-pregledi-design.md`
- [ ] Vlasnik blueprinta odlučuje o PREDLOZIMA u `docs/odluke.md` (PWA i rad bez interneta, mobilni prikaz, fotografije, push, duža sesija, biblioteke); četiri role + prava odobreno 2026-11-04
- [ ] Port 55434 upisati u registar blueprinta (03 §1)
- [x] 2026-11-04 — GitHub remote: https://github.com/DjoleLazic96/ElixirZorkaPregledi (javan)

### Faza 01 — skelet
- [x] 2026-10-27 — kopiran skelet, `preimenuj.ps1` izvršen, zaostaci „skelet" očišćeni, git init (lokalno)
- [x] 2026-11-04 — dev klaster radi, `dotnet run` + `npm run dev`, prijava `admin`
- [ ] CI zelen na prvom PR-u (posle remote-a)

### Faza 02 — prva isporuka (ceo spec)
- [x] 2026-11-04 — plan `docs/superpowers/plans/`: `00-overview.md` (faze 01–11), detaljno `01-osnova.md` i `02-oprema-i-sabloni.md`
- [x] 2026-11-04 — faza 01: četiri role, prava štikliranjem, podešavanja, sekcije, brojači (`01-osnova.md`)
- [x] 2026-11-04 — faza 02: registar opreme, katalog, dodatna polja, šabloni sa verzijama, uvoz iz .xlsx (`02-oprema-i-sabloni.md`)
- [ ] mobilni raspored (donji meni umesto bočnog) — čeka PREDLOG „mobilni prikaz“; do tada je skelet na telefonu neupotrebljiv (bočni meni 240 px)
- [x] 2026-11-04 — faza 03: raspored, kalendar, odsustva, neradni dani, prebacivanja, prenos propuštenog DP-a (`03-raspored.md`); obaveštenja o pomeranju čekaju fazu 09
- [x] 2026-11-04 — faza 04: popunjavanje sa internetom, slike, aktivno vreme, mobilni raspored (`04-popunjavanje.md`)
- [x] 2026-11-04 — faza 05: rad bez interneta, PWA, Zapamti me, jedan telefon, sporan unos, sat telefona (`05-rad-bez-interneta.md`); odluka o spornom unosu je faza 06
- [x] 2026-11-04 — faza 06: arhiva, detalj, overa, dorada, zahtev za doradu, poništavanje, sporni unosi, PDF/Word/Excel (`06-overa-i-arhiva.md`); automatski PDF/rezime pri predaji i obaveštenja su faza 09
- [x] 2026-11-04 — faza 07: akcije, pop-up, kvarovi, Neispravnosti (`07-akcije-i-kvarovi.md`); obaveštenja (hitna akcija, istekao rok) su faza 09
- [ ] faza 08: ekran uređaja, analitika, Dashboard, pretraga
- [ ] obaveštenja (push, mejl), pozadinski poslovi, pretraga, podešavanja
- [ ] korisnička uputstva po temama

### Faza 03 — druga faza: Pumpe, Trake, Plansko stajanje
- [~] Pumpe — opis i mockup gotovi (`../Druga faza/2026-10-28 Pumpe – druga faza.docx`), čekaju odgovore klijenta
- [~] Plansko stajanje — opis i mockup gotovi (`../Druga faza/2026-10-28 Plansko stajanje – druga faza.docx`), čekaju odgovore klijenta
- [~] Trake — opis i mockup gotovi (`../Druga faza/2026-10-28 Trake – druga faza.docx`), čekaju odgovore klijenta

### Zahtevi klijenta
- [ ] 2026-10-27 — Miroslav Zec / naručilac — dorada analitike posle prvih meseci rada

### Zahtevi ka IT-u
- [ ] domen `tpm-pregledi.elixirgroup.rs` + HTTPS, folder `/opt/apps/tpm-pregledi`, nalog u `docker` grupi, `proxy-net`, Passbolt → `docs/deploy/sysadmin-tasks.md`
- [ ] Microsoft Graph aplikacija (`Mail.Send`) za mejl obaveštenja
- [ ] prostor i bekap za fotografije (~5 GB godišnje)

### Odloženo
- [x] 2026-11-04 — šabloni prepisani sa najsvežijih obrazaca → `docs/sabloni/sabloni-za-uvoz.xlsx` (DP1, DP2, PZ1 doslovno + Sautelma; PZ2–PZ4 sa ispravkama korisnika); uvoz je faza 02

## Završeno

- [x] 2026-10-27 — projekat započet iz Elixir skeleta v1.1; spec napisan

## Produkcija (istorija deploy-a)

| Datum | Verzija | Image tag | Napomena |
|---|---|---|---|
| | | | |
