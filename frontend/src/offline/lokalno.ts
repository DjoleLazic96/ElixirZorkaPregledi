import type { Operacija, PopunjavanjeDto } from '../features/popunjavanje/hooks';

/**
 * Primena operacije na lokalnu kopiju pregleda (čista funkcija, bez IndexedDB-a): telefon odmah pokazuje odgovor,
 * a server ga primi kad stigne veza. Pravila su ista kao na serveru, ali server ostaje jedini izvor istine —
 * posle sinhronizacije lokalna kopija se zamenjuje serverskom, pa se neposlate operacije primene ponovo.
 */
export function primeniLokalno(p: PopunjavanjeDto, op: Operacija, sada: string): PopunjavanjeDto {
  const kopija: PopunjavanjeDto = structuredClone(p);
  switch (op.vrsta) {
    case 'zapocni':
      if (kopija.status === 'planiran') {
        kopija.status = 'u_radu';
        kopija.zapocetAt = sada;
      }
      return kopija;
    case 'odgovor':
      for (const s of kopija.sekcije)
        for (const u of s.uredjaji)
          for (const st of u.stavke)
            if (st.stavkaId === op.podaci.stavkaId)
              st.odgovor = { vrednost: op.podaci.vrednost, napomena: op.podaci.napomena ?? null, polja: op.podaci.polja ?? {} };
      return kopija;
    case 'van_funkcije':
      for (const s of kopija.sekcije)
        for (const u of s.uredjaji) if (u.uredjajId === op.podaci.uredjajId) u.vanFunkcije = op.podaci.vanFunkcije;
      return kopija;
    case 'sve_ispravno':
      for (const s of kopija.sekcije.filter((x) => x.sifra === op.podaci.sekcija))
        for (const u of s.uredjaji.filter((x) => !x.vanFunkcije))
          for (const st of u.stavke) if (!st.odgovor) st.odgovor = { vrednost: 0, napomena: null, polja: {} };
      return kopija;
    case 'akcija':
      // Broj akcije dodeljuje server; lokalno se ništa ne prikazuje dok ne stigne novi paket.
      return kopija;
    case 'akcija_odgovor':
      // Pop-up je odgovoren: akcija nestaje sa uređaja (zatvorena ili „ostaje otvorena“ za ovaj pregled).
      for (const s of kopija.sekcije)
        for (const u of s.uredjaji) u.otvoreneAkcije = u.otvoreneAkcije.filter((a) => a.id !== op.podaci.akcijaId);
      return kopija;
    case 'obrisi_sliku':
      for (const s of kopija.sekcije)
        for (const u of s.uredjaji)
          for (const st of u.stavke)
            if (st.stavkaId === op.podaci.stavkaId) st.fotografije = st.fotografije.filter((x) => x !== op.podaci.fotografijaId);
      return kopija;
    case 'predaja':
      // „Predato – čeka slanje“ postoji samo na telefonu (spec §6.4); dok se ne pošalje, pregled se ne menja.
      kopija.status = 'predato_ceka';
      return kopija;
  }
}

/** Ono što predaja traži, izračunato lokalno — da inženjer i bez interneta vidi šta nedostaje. */
export function nedostaje(p: PopunjavanjeDto): { odgovora: number; napomena: number; polja: string[] } {
  let odgovora = 0;
  let napomena = 0;
  const polja: string[] = [];
  for (const s of p.sekcije)
    for (const u of s.uredjaji.filter((x) => !x.vanFunkcije))
      for (const st of u.stavke) {
        if (!st.odgovor) {
          odgovora++;
          continue;
        }
        if (st.odgovor.vrednost === 1 && !st.odgovor.napomena?.trim()) napomena++;
        // Obavezna dodatna polja koja se vide uz ovaj odgovor (zamerka 13), isto kao na serveru.
        for (const pd of st.polja.filter((x) => x.obavezno && (x.prikaz === 'uvek' || st.odgovor!.vrednost === 1))) {
          const v = st.odgovor.polja?.[String(pd.id)];
          const ima = Array.isArray(v) ? v.length > 0 && v.every((x) => x !== null && String(x).trim() !== '') : v !== undefined && v !== null && String(v).trim() !== '';
          if (!ima) polja.push(`${pd.naziv} – ${u.naziv} ${u.oznaka}`.trim());
        }
      }
  return { odgovora, napomena, polja };
}
