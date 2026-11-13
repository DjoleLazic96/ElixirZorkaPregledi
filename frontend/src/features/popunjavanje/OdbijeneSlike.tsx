import { useEffect, useMemo, useState } from 'react';
import { useToast } from '../../app/providers/ToastProvider';
import { Button } from '../../components/ui/Button';
import { ConfirmDialog } from '../../components/ui/ConfirmDialog';
import { formatirajVreme } from '../../lib/datum';
import { smanjiSliku } from '../../lib/slika';
import { db, mozePonovo, odbaciOdbijenuSliku, posaljiPonovo, useLokalno, useOdbijeneSlike, type OdbijenaOperacija } from '../../offline/sinhronizacija';

/**
 * Slike koje server nije primio (zamerka 19). Ranije su ostajale na telefonu bez ikakvog izlaza; sada se vide na svakom
 * ekranu dok inženjer ne odluči: pošalji ponovo (dok se pregled još popunjava), sačuvaj na telefon (da je preda
 * Supervizoru) ili odbaci.
 */
export function OdbijeneSlike({ korisnikId }: { korisnikId: string | undefined }) {
  const slike = useOdbijeneSlike(korisnikId);
  if (slike.length === 0) return null;
  return (
    <section role="alert" aria-label="Neposlate slike" className="mb-2.5 space-y-2 rounded-lg border border-red-300 bg-red-50 p-3 text-sm text-red-800">
      <p className="font-semibold">
        {slike.length === 1
          ? 'Jedna slika nije primljena na serveru — sačuvana je samo na ovom telefonu.'
          : `Slike koje server nije primio: ${slike.length} — sačuvane su samo na ovom telefonu.`}
      </p>
      <ul className="space-y-2">
        {slike.map((s) => <OdbijenaSlika key={s.id} o={s} />)}
      </ul>
    </section>
  );
}

function OdbijenaSlika({ o }: { o: OdbijenaOperacija }) {
  const toast = useToast();
  const [pitaj, setPitaj] = useState(false);
  const [radi, setRadi] = useState(false);
  const broj = useLokalno(async () => (await db.pregledi.get(o.pregledId))?.broj ?? null, [o.pregledId], null as string | null);
  const ponovo = useLokalno(() => mozePonovo(o), [o.id], false);

  const url = useMemo(() => (o.slika ? URL.createObjectURL(o.slika) : null), [o.slika]);
  useEffect(() => () => { if (url) URL.revokeObjectURL(url); }, [url]);

  async function posalji() {
    setRadi(true);
    try {
      const prevelika = o.razlog.includes('prevelika');
      const ok = await posaljiPonovo(o.id!, prevelika ? (b) => smanjiSliku(new File([b], 'slika.jpg', { type: b.type || 'image/jpeg' })) : undefined);
      if (ok) toast.success('Slika je vraćena u red za slanje.');
      else toast.error('Pregled se više ne popunjava na ovom telefonu — sačuvajte sliku na telefon.');
    } finally {
      setRadi(false);
    }
  }

  const ime = `${broj ?? `pregled-${o.pregledId}`}-${o.at.slice(0, 19).replace(/[:T]/g, '-')}.jpg`;
  return (
    <li className="space-y-2 rounded-md bg-white p-2 text-gray-800">
      <div className="flex items-center gap-3">
        {url && (
          <a href={url} target="_blank" rel="noreferrer" aria-label="Otvori sliku" className="shrink-0">
            <img src={url} alt="Neposlata slika" className="h-14 w-14 rounded object-cover" />
          </a>
        )}
        <div className="min-w-0 flex-1">
          <p className="font-medium">{broj ?? `Pregled #${o.pregledId}`} · {formatirajVreme(o.at)}</p>
          <p className="text-xs text-red-700">{o.razlog}</p>
        </div>
      </div>
      <div className="flex flex-wrap justify-end gap-2">
        {ponovo && <Button size="sm" onClick={() => void posalji()} disabled={radi}>Pošalji ponovo</Button>}
        {url && (
          <a href={url} download={ime} className="inline-flex h-11 items-center md:h-10 rounded-md border border-gray-300 bg-white px-3 text-sm font-medium text-gray-700 hover:bg-gray-50">
            Sačuvaj na telefon
          </a>
        )}
        <Button size="sm" variant="ghost" onClick={() => setPitaj(true)} disabled={radi}>Odbaci</Button>
      </div>
      <ConfirmDialog
        open={pitaj}
        title="Odbacivanje slike"
        message="Slika postoji samo na ovom telefonu i biće trajno obrisana. Nastaviti?"
        confirmLabel="Odbaci sliku"
        onConfirm={() => { setPitaj(false); void odbaciOdbijenuSliku(o.id!); }}
        onCancel={() => setPitaj(false)}
      />
    </li>
  );
}
