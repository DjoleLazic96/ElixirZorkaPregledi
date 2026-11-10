import { useState } from 'react';

/** Slike uz stavku (detalj pregleda, dosije uređaja); učitavaju se tek na klik da lista ostane laka. */
export function Slike({ ids, putanja = (id) => `/fotografije/${id}` }: { ids: number[]; putanja?: (id: number) => string }) {
  if (ids.length === 0) return null;
  return (
    <span className="mt-1 flex flex-wrap gap-1">
      {ids.map((id) => <ZasticenaSlika key={id} url={putanja(id)} />)}
    </span>
  );
}

/** Slika ide kroz API sa tokenom, pa se učitava kao blob (img src ne nosi Authorization zaglavlje). */
function ZasticenaSlika({ url }: { url: string }) {
  const [src, setSrc] = useState<string | null>(null);
  const [ucitaj, setUcitaj] = useState(false);
  if (!ucitaj)
    return <button type="button" className="h-11 min-w-11 rounded border border-gray-300 px-2 text-xs text-primary-700" onClick={() => {
      setUcitaj(true);
      void import('../api/client').then(({ api }) => api.get<Blob>(url, { responseType: 'blob' })).then((r) => setSrc(URL.createObjectURL(r.data)));
    }}>Slika</button>;
  return src ? <a href={src} target="_blank" rel="noreferrer"><img src={src} alt="Slika uz stavku" className="h-16 w-16 rounded object-cover" /></a> : <span className="text-xs text-gray-500">…</span>;
}
