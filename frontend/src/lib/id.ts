/**
 * Nasumični UUID v4 za `opId`, ID slike i ID instalacije. `crypto.randomUUID` postoji samo na HTTPS-u (i localhost-u); na `http://`
 * adresi u lokalnoj mreži (proba na telefonu) ga nema, pa se UUID sklapa iz `crypto.getRandomValues`, koji radi svuda.
 */
export function noviId(): string {
  if (typeof crypto.randomUUID === 'function') return crypto.randomUUID();
  const b = crypto.getRandomValues(new Uint8Array(16));
  b[6] = (b[6] & 0x0f) | 0x40;
  b[8] = (b[8] & 0x3f) | 0x80;
  const h = [...b].map((x) => x.toString(16).padStart(2, '0')).join('');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}
