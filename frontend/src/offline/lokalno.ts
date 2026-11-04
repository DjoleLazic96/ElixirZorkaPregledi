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
    case 'predaja':
      // „Predato – čeka slanje“ postoji samo na telefonu (spec §6.4); dok se ne pošalje, pregled se ne menja.
      kopija.status = 'predato_ceka';
      return kopija;
  }
}

/** Ono što predaja traži, izračunato lokalno — da inženjer i bez interneta vidi šta nedostaje. */
export function nedostaje(p: PopunjavanjeDto): { odgovora: number; napomena: number } {
  let odgovora = 0;
  let napomena = 0;
  for (const s of p.sekcije)
    for (const u of s.uredjaji.filter((x) => !x.vanFunkcije))
      for (const st of u.stavke) {
        if (!st.odgovor) odgovora++;
        else if (st.odgovor.vrednost === 1 && !st.odgovor.napomena?.trim()) napomena++;
      }
  return { odgovora, napomena };
}
