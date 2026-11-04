# Istorija izmena — TPM pregledi

Format po Keep a Changelog, verzije po SemVer (Elixir blueprint 11). Pisano za korisnike aplikacije:
šta vide i šta se promenilo, ne tehnički detalji.

## [Neobjavljeno]

### Novo
- **Šabloni**: šest šablona pregleda (DP1, DP2, PZ1–PZ4) prikazani kao papirni obrasci, katalog aktivnosti i dodatna polja; administrator ih uvozi iz Excela.
- **Uređaji**: registar opreme sa pretragom, filterima i statusom „van upotrebe“.
- Četiri role: Inženjer, Supervizor, Direktor i Administrator; administrator svakom korisniku štiklira prava (**Administracija → Korisnici → Prava**).
- **Administracija → Podešavanja**: vremena upozorenja, kraj radnog dana, granica pauze, rok za overu i pragovi obaveštenja.

### Izmenjeno
- Uklonjen primer „Kategorije“ iz skeleta; početni ekran je **Šabloni**.

### Ispravljeno
- 

### Bezbednost
- 

### Napomene za nadogradnju
- Postojeći nalozi sa rolom „Korisnik“ postaju „Inženjer“; nalozi bez prava dobijaju podrazumevana prava svoje role pri prvom startu.
- 

## [0.1.0] — 2026-10-12

### Novo
- Početni tpm-pregledi: prijava, korisnici i role, promena lozinke, audit log, stanje servera, verzija aplikacije
  („O aplikaciji"), primer šifarnika „Kategorije".
