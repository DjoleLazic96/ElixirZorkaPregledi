import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Badge } from '../../components/ui/Badge';
import { Card, CardBody, CardHeader, CardTitle } from '../../components/ui/Card';
import { Lista, StavkaListe } from '../../components/ui/Lista';
import { Ucitavanje } from '../../components/ui/Spinner';
import { porukaGreske } from '../../lib/apiError';
import { cn } from '../../lib/cn';
import { danasYmd, formatirajDatum, formatirajDatumDug, relativnoVreme } from '../../lib/datum';
import { formatirajProcenat } from '../../lib/broj';
import { useAuth } from '../auth/AuthProvider';
import { hitnostLabela, tonHitnosti } from '../akcije/oznake';
import { statusLabela, tonStatusa } from '../pregledi/hooks';
import { useDashboard, type OdlukaDto, type PregledKratkoDto } from './hooks';
import { Plocica } from './Prikazi';

/** Moj današnji ili započet pregled vodi na popunjavanje, ostali na detalj. */
const linkPregleda = (p: PregledKratkoDto) =>
  p.moj && ['planiran', 'u_radu', 'vracen_na_doradu'].includes(p.status) ? `/pregled/${p.id}/popuni` : `/pregledi/${p.id}`;

const IKONA_ODLUKE: Record<string, string> = { overa: '✅', dorada: '↩', prebacivanje: '🔁', odsustvo: '🏖️', predlog: '🧩', odsutan: '⚠️', bez_prava: '⛔' };

function pozdrav(): string {
  const sat = new Date().getHours();
  return sat < 12 ? 'Dobro jutro' : sat < 18 ? 'Dobar dan' : 'Dobro veče';
}

function StavkePregleda({ pregledi, prazno, prebacivanje = false }: { pregledi: PregledKratkoDto[]; prazno: string; prebacivanje?: boolean }) {
  if (pregledi.length === 0) return <p className="text-xs text-gray-500">{prazno}</p>;
  return (
    <>
      {pregledi.map((p) => (
        <StavkaListe key={p.id} broj={p.broj} to={linkPregleda(p)} moje={p.moj}
          naslov={`${p.tip} · ${formatirajDatum(p.datum)}`}
          opis={`Dodeljeno: ${p.inzenjer}${p.overio ? ` · overio ${p.overio}` : ''}`}
          desno={<>
            {p.inzenjerOdsutan && <Badge tone="red">inženjer odsutan</Badge>}
            <Badge tone={tonStatusa(p.status)}>{statusLabela(p.status)}</Badge>
            {prebacivanje && (p.zahtevNaCekanju
              ? <Badge tone="amber">zahtev poslat</Badge>
              : <Link to={`/kalendar?pregled=${p.id}&datum=${p.datum}`} className="inline-flex h-11 items-center rounded-lg border border-gray-200 bg-white px-3 text-xs font-medium text-gray-800 hover:bg-gray-100 md:h-8">Zatraži prebacivanje</Link>)}
          </>} />
      ))}
    </>
  );
}

function StavkeOdluka({ odluke, prazno }: { odluke: OdlukaDto[]; prazno: string }) {
  if (odluke.length === 0) return <p className="text-xs text-gray-500">{prazno}</p>;
  return (
    <>
      {odluke.map((o, i) => (
        <StavkaListe key={i} ikona={IKONA_ODLUKE[o.vrsta]} to={o.link} naslov={o.opis}
          opis={o.od ? relativnoVreme(o.od) : undefined}
          desno={o.rokIstekao ? <Badge tone="red">rok istekao</Badge> : undefined} />
      ))}
    </>
  );
}

function Kartica({ naslov, children, className }: { naslov: ReactNode; children: ReactNode; className?: string }) {
  return (
    <Card className={className}>
      <CardHeader><CardTitle className="flex items-center gap-2">{naslov}</CardTitle></CardHeader>
      <CardBody>{children}</CardBody>
    </Card>
  );
}

/**
 * Početna po mockup-u (`scrDash`, R8.10–R8.12): pozdrav i datum; ko popunjava ima „Moje aktivnosti“ (svetlo zelena linija levo),
 * Supervizor i Direktor „Čeka moju odluku“; zatim brojevi i kartice. Svi vide sve, uz oznake kome je dodeljeno i ko je overio.
 */
export function DashboardPage() {
  const d = useDashboard();
  const { me } = useAuth();
  if (d.isLoading) return <Ucitavanje />;
  if (!d.data) return <p role="alert" className="text-sm text-red-600">{porukaGreske(d.error, 'Početna nije učitana.')}</p>;
  const { moje, cekaOdluku, kpi } = d.data;
  const ime = me?.user.fullName.split(' ')[0] ?? '';
  const datum = formatirajDatumDug(danasYmd());

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-[19px] font-semibold leading-tight text-gray-800 md:text-[22px]">{pozdrav()}{ime && `, ${ime}`}</h1>
        <p className="mt-1 text-sm text-gray-500">{datum.charAt(0).toUpperCase() + datum.slice(1)}</p>
      </div>

      {moje && (
        <Kartica naslov="Moje aktivnosti" className="border-l-4 border-l-accent-500">
          <Lista>
            <StavkePregleda pregledi={moje.danas} prazno="Danas nemate pregled." />
            {moje.zapoceti.filter((z) => !moje.danas.some((x) => x.id === z.id)).length > 0 && (
              <StavkePregleda pregledi={moje.zapoceti.filter((z) => !moje.danas.some((x) => x.id === z.id))} prazno="" />
            )}
            <StavkePregleda pregledi={moje.narednih7} prazno="Nema planiranih pregleda u narednih 7 dana." prebacivanje />
            {moje.zahtevi.map((z, i) => (
              <StavkaListe key={`z${i}`} ikona={IKONA_ODLUKE[z.vrsta] ?? '⏳'} to={z.link} naslov={z.opis}
                opis={z.od ? relativnoVreme(z.od) : undefined} desno={<Badge tone="yellow">Na čekanju</Badge>} />
            ))}
            {moje.akcije.length > 0 && (
              <StavkaListe ikona="🛠️" to="/akcije" naslov={`Akcije na uređajima iz mojih pregleda: ${moje.akcije.length}`}
                opis="Pop-up iskače kad dođete do uređaja" />
            )}
          </Lista>
        </Kartica>
      )}

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-6">
        <Plocica vrednost={formatirajProcenat(kpi.procenatNaDan)} naslov="ispunjenje plana (mesec)" to="/analitika" />
        <Plocica vrednost={String(kpi.cekaOveru)} naslov="čeka overu" to="/pregledi" />
        <Plocica vrednost={String(d.data.akcijePoHitnosti.hitno ?? 0)} naslov="hitnih akcija" upozorenje={(d.data.akcijePoHitnosti.hitno ?? 0) > 0} to="/akcije" />
        <Plocica vrednost={String(kpi.otvorenihAkcija)} naslov="otvorenih akcija" to="/akcije" />
        <Plocica vrednost={String(kpi.otvorenihKvarova)} naslov="otvorenih kvarova" to="/neispravnosti" />
        <Plocica vrednost={String(kpi.vanFunkcije)} naslov="uređaja van funkcije" to="/uredjaji" />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Kartica naslov="Danas i sutra">
          <Lista>
            <StavkePregleda pregledi={[...d.data.danas, ...d.data.sutra]} prazno="Danas i sutra nema pregleda." />
          </Lista>
        </Kartica>
        {cekaOdluku ? (
          <Kartica naslov={<>Čeka moju odluku <Badge tone={cekaOdluku.length ? 'red' : 'gray'}>{cekaOdluku.length}</Badge></>}>
            <Lista><StavkeOdluka odluke={cekaOdluku} prazno="Nema ničega što čeka vašu odluku." /></Lista>
          </Kartica>
        ) : (
          <Kartica naslov="Čeka overu">
            <Lista><StavkePregleda pregledi={d.data.cekajuOveru} prazno="Nijedan pregled ne čeka overu." /></Lista>
          </Kartica>
        )}
        <Kartica naslov="Otvorene akcije po hitnosti">
          {d.data.akcije.length === 0 ? <p className="text-xs text-gray-500">Nema otvorenih akcija.</p> : (
            <Lista>
              {d.data.akcije.map((a) => (
                <StavkaListe key={a.id} broj={a.broj} to={`/akcije/${a.id}`} naslov={a.uredjaj}
                  opis={<>{a.aktivnost} · prijavio {a.prijavio}{a.rok && <span className={cn(a.rokIstekao && 'font-semibold text-red-700')}> · rok {formatirajDatum(a.rok)}</span>}</>}
                  desno={<>
                    {a.rokIstekao && <Badge tone="red">rok istekao</Badge>}
                    {a.stavkaSadaIspravna && <Badge tone="green">stavka sada ispravna</Badge>}
                    <Badge tone={tonHitnosti(a.hitnost)}>{hitnostLabela(a.hitnost)}</Badge>
                  </>} />
              ))}
            </Lista>
          )}
          <Link className="mt-2.5 inline-block text-sm font-semibold text-primary-500 hover:underline" to="/akcije">Sve akcije ›</Link>
        </Kartica>
        <Kartica naslov="Otvoreni kvarovi">
          {d.data.kvarovi.length === 0 ? <p className="text-xs text-gray-500">Nema otvorenih kvarova.</p> : (
            <Lista>
              {d.data.kvarovi.map((k) => (
                <StavkaListe key={k.id} broj={k.broj} to={`/neispravnosti/${k.id}`} naslov={k.uredjaj}
                  opis={`${k.aktivnost} · od ${formatirajDatum(k.otvoren)}`} desno={<Badge tone="red">otvoren {k.trajanjeDana} d</Badge>} />
              ))}
            </Lista>
          )}
          <Link className="mt-2.5 inline-block text-sm font-semibold text-primary-500 hover:underline" to="/neispravnosti">Sve neispravnosti ›</Link>
        </Kartica>
        {cekaOdluku && (
          <Kartica naslov="Čeka overu">
            <Lista><StavkePregleda pregledi={d.data.cekajuOveru} prazno="Nijedan pregled ne čeka overu." /></Lista>
          </Kartica>
        )}
        <Kartica naslov="Uređaji van funkcije">
          {d.data.vanFunkcije.length === 0 ? <p className="text-xs text-gray-500">Svi uređaji su u funkciji.</p> : (
            <Lista>
              {d.data.vanFunkcije.map((v) => (
                <StavkaListe key={v.uredjajId} to={`/uredjaji/${v.uredjajId}`} naslov={v.uredjaj}
                  opis={`van funkcije od ${formatirajDatum(v.od)} (${v.dana} d)`} />
              ))}
            </Lista>
          )}
        </Kartica>
      </div>
    </div>
  );
}
