# TODO — TPM pregledi

> Radni dnevnik projekta. **Status je ovde**, ne u planovima. Oznake: `[ ]` otvoreno, `[~]` u toku, `[x]` gotovo.
> Odluke se zapisuju u kontekstu kao **ODLUKA** i prepisuju u `docs/odluke.md`.

## Otvoreno

### Pre početka razvoja
- [ ] Vlasnik projekta (Miroslav Zec) odobrava spec `docs/superpowers/specs/2026-10-27-tpm-pregledi-design.md`
- [ ] Vlasnik blueprinta odlučuje o PREDLOZIMA u `docs/odluke.md` (PWA i rad bez interneta, mobilni prikaz, fotografije, push, četiri role + prava, duža sesija, biblioteke)
- [ ] Licenca za PDF (`QuestPDF` komercijalna ili druga biblioteka)
- [ ] Port 55434 upisati u registar blueprinta (03 §1)
- [ ] GitHub organizacija i remote (odluka vlasnika projekta)

### Faza 01 — skelet
- [x] 2026-10-27 — kopiran skelet, `preimenuj.ps1` izvršen, zaostaci „skelet" očišćeni, git init (lokalno)
- [ ] dev klaster radi, `dotnet run` + `npm run dev`, prijava `admin`
- [ ] CI zelen na prvom PR-u (posle remote-a)

### Faza 02 — prva isporuka (ceo spec)
- [ ] plan `docs/superpowers/plans/` (višefazni: `00-overview.md` + planovi po celinama)
- [ ] role i prava; registar opreme, katalog, šabloni sa verzijama i uvoz
- [ ] raspored, kalendar, odsustva, neradni dani, prebacivanja
- [ ] popunjavanje i rad bez interneta (PWA, sinhronizacija, slike)
- [ ] predaja, overa, dorada, poništavanje; izveštaji PDF/Word
- [ ] akcije, kvarovi, ekran uređaja, analitika, Dashboard
- [ ] obaveštenja (push, mejl), pozadinski poslovi, pretraga, podešavanja
- [ ] korisnička uputstva po temama

### Faza 03 — druga faza: Pumpe, Trake, Plansko stajanje
- [~] Pumpe — opis i mockup gotovi (`../Druga faza/2026-10-28 Pumpe – druga faza.docx`), čekaju odgovore klijenta
- [~] Plansko stajanje — opis i mockup gotovi (`../Druga faza/2026-10-28 Plansko stajanje – druga faza.docx`), čekaju odgovore klijenta
- [ ] Trake — još nisu opisane

### Zahtevi klijenta
- [ ] 2026-10-27 — Miroslav Zec / naručilac — dorada analitike posle prvih meseci rada

### Zahtevi ka IT-u
- [ ] domen `tpm-pregledi.elixirgroup.rs` + HTTPS, folder `/opt/apps/tpm-pregledi`, nalog u `docker` grupi, `proxy-net`, Passbolt → `docs/deploy/sysadmin-tasks.md`
- [ ] Microsoft Graph aplikacija (`Mail.Send`) za mejl obaveštenja
- [ ] prostor i bekap za fotografije (~5 GB godišnje)

### Odloženo
- [ ] sređivanje šablona (ručne ispravke iz `../Analiza napomena DP-PZ.xlsx`) i uvoz ažurne verzije

## Završeno

- [x] 2026-10-27 — projekat započet iz Elixir skeleta v1.1; spec napisan

## Produkcija (istorija deploy-a)

| Datum | Verzija | Image tag | Napomena |
|---|---|---|---|
| | | | |
