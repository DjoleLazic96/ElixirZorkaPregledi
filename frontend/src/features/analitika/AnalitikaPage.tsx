import { useState } from 'react';
import { Card, CardBody, CardHeader, CardTitle } from '../../components/ui/Card';
import { DatePicker } from '../../components/ui/DatePicker';
import { PageHeader } from '../../components/ui/PageHeader';
import { Ucitavanje } from '../../components/ui/Spinner';
import { cn } from '../../lib/cn';
import { danasYmd, dodajDane } from '../../lib/datum';
import { formatirajBroj, formatirajProcenat } from '../../lib/broj';
import { useIspunjenje, useOpstaAnalitika, type IspunjenjeRedDto, type TrajanjeDto } from './hooks';
import { Plocica, Stubici } from './Prikazi';

/** Opšta analitika (R8.8) i ispunjenje plana (R8.9) za izabrani period. */
export function AnalitikaPage() {
  const [od, setOd] = useState(() => dodajDane(danasYmd(), -89));
  const [doDana, setDo] = useState(danasYmd);
  const opsta = useOpstaAnalitika(od, doDana);
  const isp = useIspunjenje(od, doDana);
  const a = opsta.data;
  const i = isp.data;

  return (
    <div className="space-y-4">
      <PageHeader title="Analitika" />
      <Card>
        <CardBody className="flex flex-wrap items-end gap-3 text-sm">
          <label className="space-y-1"><span className="block text-gray-600">Od</span><DatePicker value={od} max={doDana} onChange={setOd} aria-label="Od" /></label>
          <label className="space-y-1"><span className="block text-gray-600">Do</span><DatePicker value={doDana} min={od} onChange={setDo} aria-label="Do" /></label>
        </CardBody>
      </Card>

      {!a || !i ? <Ucitavanje /> : (
        <div className={cn('space-y-4', (opsta.isFetching || isp.isFetching) && 'opacity-60')}>
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
            <Plocica naslov="Pregledi na planirani dan" vrednost={formatirajProcenat(i.ukupno.procenatNaDan, 1)} opis={`${i.ukupno.naDan} od ${i.ukupno.planirano}`} />
            <Plocica naslov="Overeni u roku" vrednost={formatirajProcenat(i.ukupno.procenatURoku, 1)} opis={`prosečno ${formatirajBroj(i.ukupno.prosekOvereSati, 1)} h do overe`} />
            <Plocica naslov="Kvarova u periodu" vrednost={String(a.kvarova)} />
            <Plocica naslov="Prosečno trajanje kvara" vrednost={`${formatirajBroj(a.prosekTrajanjaKvara, 1)} d`} />
          </div>

          <Card>
            <CardHeader><CardTitle>Ispunjenje plana po mesecu</CardTitle></CardHeader>
            <CardBody><TabelaIspunjenja redovi={i.ukupno.planirano ? [...i.poMesecu, i.ukupno] : []} prva="Mesec" /></CardBody>
          </Card>
          <Card>
            <CardHeader><CardTitle>Ispunjenje plana po inženjeru</CardTitle></CardHeader>
            <CardBody>
              <TabelaIspunjenja redovi={i.poInzenjeru} prva="Inženjer" />
              <p className="mt-2 text-xs text-gray-500">Dani odsustva se ne računaju inženjeru; preneti pregled se računa onom kome je bio dodeljen.</p>
            </CardBody>
          </Card>

          <div className="grid gap-4 lg:grid-cols-2">
            <Card><CardHeader><CardTitle>Kvarovi po sekciji</CardTitle></CardHeader><CardBody><Stubici stavke={a.poSekciji.map((s) => ({ ...s, naziv: `Sekcija ${s.naziv}` }))} /></CardBody></Card>
            <Card><CardHeader><CardTitle>Kvarovi po mesecu</CardTitle></CardHeader><CardBody><Stubici stavke={a.poMesecu} /></CardBody></Card>
            <Card><CardHeader><CardTitle>Uređaji sa najviše kvarova</CardTitle></CardHeader><CardBody><Stubici stavke={a.poUredjaju} /></CardBody></Card>
            <Card><CardHeader><CardTitle>Aktivnosti sa najviše kvarova</CardTitle></CardHeader><CardBody><Stubici stavke={a.poAktivnosti} /></CardBody></Card>
            <Card><CardHeader><CardTitle>Najčešći kvarovi (uređaj i aktivnost)</CardTitle></CardHeader><CardBody><Stubici stavke={a.najcesciKvarovi} /></CardBody></Card>
            <Card><CardHeader><CardTitle>„Nije u funkciji“ po uređaju (pregleda)</CardTitle></CardHeader><CardBody><Stubici stavke={a.vanFunkcije} /></CardBody></Card>
            <Card><CardHeader><CardTitle>Trajanje pregleda po tipu</CardTitle></CardHeader><CardBody><Trajanja stavke={a.trajanjePoTipu} /></CardBody></Card>
            <Card><CardHeader><CardTitle>Trajanje pregleda po inženjeru</CardTitle></CardHeader><CardBody><Trajanja stavke={a.trajanjePoInzenjeru} /></CardBody></Card>
          </div>
        </div>
      )}
    </div>
  );
}

function Trajanja({ stavke }: { stavke: TrajanjeDto[] }) {
  return <Stubici jedinica=" min" stavke={stavke.map((t) => ({ naziv: `${t.naziv} (${t.pregleda})`, broj: t.prosekMinuta, id: null }))} />;
}

function TabelaIspunjenja({ redovi, prva }: { redovi: IspunjenjeRedDto[]; prva: string }) {
  if (redovi.length === 0) return <p className="text-sm text-gray-500">Nema planiranih pregleda u periodu.</p>;
  const kol = ['Planirano', 'Na dan', 'Kasnije', 'Propušteno', 'Preneto', '% na dan', 'Overeno u roku', '% u roku', 'Do overe (h)'];
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead className="text-xs text-gray-500">
          <tr><th className="px-2 py-2 text-left">{prva}</th>{kol.map((k) => <th key={k} className="whitespace-nowrap px-2 py-2 text-right">{k}</th>)}</tr>
        </thead>
        <tbody className="divide-y divide-gray-100 tabular-nums">
          {redovi.map((r) => (
            <tr key={r.naziv} className={cn(r.naziv === 'Ukupno' && 'font-semibold')}>
              <td className="px-2 py-1.5">{r.naziv}</td>
              {[r.planirano, r.naDan, r.kasnije, r.propusteno, r.preneto].map((v, ix) => <td key={ix} className="px-2 py-1.5 text-right">{v}</td>)}
              <td className="px-2 py-1.5 text-right">{formatirajProcenat(r.procenatNaDan, 1)}</td>
              <td className="px-2 py-1.5 text-right">{r.overenoURoku} / {r.overeno}</td>
              <td className="px-2 py-1.5 text-right">{formatirajProcenat(r.procenatURoku, 1)}</td>
              <td className="px-2 py-1.5 text-right">{formatirajBroj(r.prosekOvereSati, 1)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
