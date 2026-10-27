import { useEffect, useState, type ReactNode } from 'react';
import { RefreshCw } from 'lucide-react';
import type { ServerStanje } from '../../api/types';
import { Button } from '../../components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '../../components/ui/Card';
import { FetchingOverlay } from '../../components/ui/FetchingOverlay';
import { PageHeader } from '../../components/ui/PageHeader';
import { ProgressTraka } from '../../components/ui/ProgressTraka';
import { formatirajBajte, formatirajBroj, formatirajProcenat } from '../../lib/broj';
import { cn } from '../../lib/cn';
import { formatirajVremeUtc } from '../../lib/datum';
import { useServerStanje } from './hooks';

/**
 * Stranica „Stanje servera" (blueprint 10 §2, samo Admin): tri kartice —
 * Aplikacija, Baza, Host — sa vizuelnim pragovima (samo boja, bez alarma).
 * Prvo učitavanje: skeleton kartice; naredna: `FetchingOverlay`.
 */

const NEDOSTUPNO = 'nedostupno';

/** Pragovi iz blueprinta 10 §2 (žuto / crveno). */
const PRAG = {
  cpu: { warnAt: 70, dangerAt: 90 },
  ram: { warnAt: 75, dangerAt: 90 },
  diskSlobodno: { warnAt: 20, dangerAt: 10 },
};

/** Sekunde → „1 d 2 h 3 min" (vodeće nule se izostavljaju). */
function formatirajUptime(sekundi: number): string {
  const d = Math.floor(sekundi / 86_400);
  const h = Math.floor((sekundi % 86_400) / 3_600);
  const min = Math.floor((sekundi % 3_600) / 60);
  const delovi: string[] = [];
  if (d > 0) delovi.push(`${d} d`);
  if (h > 0 || d > 0) delovi.push(`${h} h`);
  delovi.push(`${min} min`);
  return delovi.join(' ');
}

function procenat(deo: number | null, celina: number | null): number | null {
  if (deo === null || celina === null || celina === 0) return null;
  return (deo / celina) * 100;
}

function bajte(n: number | null): string {
  return n === null ? NEDOSTUPNO : formatirajBajte(n);
}

/** Jedan red „labela | vrednost" u kartici. */
function Red({ labela, vrednost }: { labela: string; vrednost: ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-3 py-1.5">
      <span className="text-xs uppercase tracking-wide text-gray-500">{labela}</span>
      <span className="text-right text-sm font-medium tabular-nums text-gray-800">{vrednost}</span>
    </div>
  );
}

/** Red sa trakom napretka ispod vrednosti (CPU, RAM, disk). */
function RedSaTrakom({
  labela,
  vrednost,
  procenat: p,
  prag,
  obrnuto,
}: {
  labela: string;
  vrednost: ReactNode;
  procenat: number | null;
  prag: { warnAt: number; dangerAt: number };
  obrnuto?: boolean;
}) {
  return (
    <div className="space-y-1.5 py-1.5">
      <div className="flex items-start justify-between gap-3">
        <span className="text-xs uppercase tracking-wide text-gray-500">{labela}</span>
        <span className="text-right text-sm font-medium tabular-nums text-gray-800">{vrednost}</span>
      </div>
      <ProgressTraka
        percent={p}
        warnAt={prag.warnAt}
        dangerAt={prag.dangerAt}
        inverted={obrnuto}
        aria-label={labela}
      />
    </div>
  );
}

function AplikacijaKartica({ a }: { a: ServerStanje['aplikacija'] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Aplikacija</CardTitle>
      </CardHeader>
      <CardBody className="divide-y divide-gray-100">
        <Red labela="Verzija" vrednost={a.verzija} />
        <Red labela="Git SHA" vrednost={<code>{a.gitSha}</code>} />
        <Red labela=".NET" vrednost={a.dotnetVerzija} />
        <Red labela="Okruženje" vrednost={a.okruzenje} />
        <Red labela="Radi neprekidno" vrednost={formatirajUptime(a.uptimeSekundi)} />
        <Red labela="Vreme servera" vrednost={formatirajVremeUtc(a.vremeUtc)} />
      </CardBody>
    </Card>
  );
}

function BazaKartica({ b }: { b: ServerStanje['baza'] }) {
  return (
    <Card className={cn(!b.veza && 'border-red-300 bg-red-50')}>
      <CardHeader>
        <CardTitle>Baza</CardTitle>
      </CardHeader>
      <CardBody className="divide-y divide-gray-100">
        <Red
          labela="Veza"
          vrednost={
            <span className="inline-flex items-center gap-1.5">
              <span
                className={cn('inline-block h-2.5 w-2.5 rounded-full', b.veza ? 'bg-primary-500' : 'bg-red-500')}
                aria-hidden
              />
              {b.veza ? 'Povezano' : 'Nije povezano'}
            </span>
          }
        />
        {!b.veza && b.greska && (
          <Red labela="Greška" vrednost={<span className="text-red-600">{b.greska}</span>} />
        )}
        <Red labela="Postgres verzija" vrednost={b.postgresVerzija ?? NEDOSTUPNO} />
        <Red labela="Veličina baze" vrednost={bajte(b.velicinaBazeBytes)} />
        <Red
          labela="Aktivne konekcije"
          vrednost={b.aktivneKonekcije === null ? NEDOSTUPNO : formatirajBroj(b.aktivneKonekcije, 0)}
        />
        <Red
          labela="Odziv"
          vrednost={b.odzivMs === null ? NEDOSTUPNO : `${formatirajBroj(b.odzivMs, 0)} ms`}
        />
        {/* null i kad backup postoji a folder nije čitljiv — zato „nepoznato", ne „nema". */}
        <Red
          labela="Poslednji backup"
          vrednost={b.poslednjiBackupUtc ? formatirajVremeUtc(b.poslednjiBackupUtc) : 'nepoznato'}
        />
      </CardBody>
    </Card>
  );
}

function HostKartica({ h }: { h: ServerStanje['host'] }) {
  const ramProcenat = h ? procenat(h.ramIskoriscenoBytes, h.ramUkupnoBytes) : null;
  const diskProcenat = h ? procenat(h.diskSlobodnoBytes, h.diskUkupnoBytes) : null;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Host</CardTitle>
      </CardHeader>
      <CardBody className="divide-y divide-gray-100">
        {h === null ? (
          <p className="py-2 text-sm italic text-gray-500">Metrike hosta su {NEDOSTUPNO}.</p>
        ) : (
          <>
            <Red labela="Operativni sistem" vrednost={h.osOpis || NEDOSTUPNO} />
            <RedSaTrakom
              labela="Procesor (CPU)"
              vrednost={h.cpuProcenat === null ? NEDOSTUPNO : formatirajProcenat(h.cpuProcenat, 1)}
              procenat={h.cpuProcenat}
              prag={PRAG.cpu}
            />
            <RedSaTrakom
              labela="Memorija (RAM)"
              vrednost={
                <>
                  {bajte(h.ramIskoriscenoBytes)}
                  <span className="text-gray-400"> / {bajte(h.ramUkupnoBytes)}</span>
                  {ramProcenat !== null && (
                    <span className="ml-1 text-xs text-gray-500">({formatirajProcenat(ramProcenat)})</span>
                  )}
                </>
              }
              procenat={ramProcenat}
              prag={PRAG.ram}
            />
            <RedSaTrakom
              labela="Disk (slobodno)"
              vrednost={
                <>
                  {bajte(h.diskSlobodnoBytes)}
                  <span className="text-gray-400"> / {bajte(h.diskUkupnoBytes)}</span>
                  {diskProcenat !== null && (
                    <span className="ml-1 text-xs text-gray-500">({formatirajProcenat(diskProcenat)})</span>
                  )}
                </>
              }
              procenat={diskProcenat}
              prag={PRAG.diskSlobodno}
              obrnuto
            />
          </>
        )}
      </CardBody>
    </Card>
  );
}

/** Skeleton kartice za prvo učitavanje (nema podataka „ispod" koje bi FetchingOverlay zatamnio). */
function SkeletonKartice() {
  return (
    <div role="status" aria-label="Učitavanje…" className="grid gap-4 lg:grid-cols-3">
      {['Aplikacija', 'Baza', 'Host'].map((naslov) => (
        <Card key={naslov}>
          <CardHeader>
            <CardTitle>{naslov}</CardTitle>
          </CardHeader>
          <CardBody className="space-y-3">
            {[0, 1, 2, 3].map((i) => (
              <div key={i} className="h-4 animate-pulse rounded bg-gray-100 motion-reduce:animate-none" />
            ))}
          </CardBody>
        </Card>
      ))}
    </div>
  );
}

/** „Osveženo pre N s" — otkucava svake sekunde od poslednjeg uspešnog odgovora. */
function useSekundiOd(trenutak: number | undefined): number | null {
  const [sada, setSada] = useState(() => Date.now());
  useEffect(() => {
    const id = window.setInterval(() => setSada(Date.now()), 1_000);
    return () => window.clearInterval(id);
  }, []);
  if (!trenutak) return null;
  return Math.max(0, Math.round((sada - trenutak) / 1_000));
}

export function StanjeServeraPage() {
  const upit = useServerStanje();
  const s = upit.data;
  const pre = useSekundiOd(upit.dataUpdatedAt || undefined);

  return (
    <div className="space-y-4">
      <PageHeader
        title="Stanje servera"
        description="Tehnički pregled za IT. Osvežava se automatski svakih 15 sekundi."
        actions={
          <>
            {pre !== null && (
              <span className="text-xs tabular-nums text-gray-500">Osveženo pre {pre} s</span>
            )}
            <Button variant="secondary" onClick={() => void upit.refetch()} disabled={upit.isFetching}>
              <RefreshCw size={14} className={cn(upit.isFetching && 'animate-spin motion-reduce:animate-none')} aria-hidden />
              Osveži
            </Button>
          </>
        }
      />

      {upit.isError && !s && (
        <div className="flex items-center justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <span>Učitavanje stanja servera nije uspelo.</span>
          <Button variant="secondary" size="sm" onClick={() => void upit.refetch()}>
            Pokušaj ponovo
          </Button>
        </div>
      )}

      {upit.isPending && <SkeletonKartice />}

      {s && (
        <FetchingOverlay fetching={upit.isFetching}>
          <div className="grid gap-4 lg:grid-cols-3">
            <AplikacijaKartica a={s.aplikacija} />
            <BazaKartica b={s.baza} />
            <HostKartica h={s.host} />
          </div>
        </FetchingOverlay>
      )}
    </div>
  );
}
