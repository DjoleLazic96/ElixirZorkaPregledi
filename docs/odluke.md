# Odluke — TPM pregledi

> Dnevnik odluka koje nisu očigledne iz koda: izbor između opcija, odstupanja od Elixir blueprinta, dogovori
> sa klijentom i IT-om. Jedna sekcija po odluci, najnovija na vrhu. Odluka koja odstupa od blueprinta se
> **mora** naći ovde pre implementacije (blueprint 19 §7).

---

## 2026-10-12 — Identitet projekta

**Kontekst:** početak projekta iz Elixir tpm-pregledia.
**Odluka:** naziv „TPM pregledi", slug `tpm-pregledi`, namespace `TpmPregledi`, domen `tpm-pregledi.elixirgroup.rs`, dev port `55434`.
**Posledice:** imena compose projekta, image-a, baze, issuer-a, cookie-ja i mrežnog aliasa izvedena iz sluga.
**Alternative odbijene:** —
**Odlučio:** vlasnik projekta.

## 2026-10-12 — Role

**Kontekst:** tpm-pregledi dolazi sa dve role.
**Odluka:** `Admin` (sve) i `Korisnik` (čita i radi, ne administrira); jedna rola po korisniku.
**Posledice:** politike `RequireAdmin`, `RequireKorisnik`; matrica u `docs/04`. Nova rola = konstanta + politika + seed + matrica + `permissions.ts` + testovi.
**Alternative odbijene:** više rola po korisniku — nema potrebe u procesu.
**Odlučio:** vlasnik projekta.

<!-- Šablon:
## YYYY-MM-DD — Naslov
**Kontekst:** zašto se odluka uopšte donosi.
**Odluka:** šta je odlučeno, jednom rečenicom.
**Posledice:** šta to menja u kodu/docs/procesu; ako odstupa od blueprinta — od kog dokumenta i zašto.
**Alternative odbijene:** šta smo još razmatrali i zašto ne.
**Odlučio:** ko (vlasnik projekta / vlasnik blueprinta).

Primer odstupanja:
## 2026-11-03 — Više rola po korisniku
**Kontekst:** klijent traži da šef smene bude i „Analitičar" i „Smenovođa".
**Odluka:** korisnik ima skup rola (`roles[]`) umesto jedne (`rola`); odstupanje od blueprinta 07 §3.
**Posledice:** `KorisnikDto.role: string[]`, forma sa checkbox-ovima („bar jedna"), guard „poslednji admin" gleda skup; `docs/04` ažuriran.
**Alternative odbijene:** treća kombinovana rola `SefSmene` — eksplozija kombinacija kad stigne četvrta.
**Odlučio:** vlasnik projekta + vlasnik blueprinta (2026-11-03).
-->
