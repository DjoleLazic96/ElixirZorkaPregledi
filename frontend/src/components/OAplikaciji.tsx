import { useQuery } from '@tanstack/react-query';
import { AlertTriangle } from 'lucide-react';
import { api } from '../api/client';
import type { VersionDto } from '../api/types';
import { formatirajVremeUtc } from '../lib/datum';
import { GIT_SHA, VERZIJA } from '../lib/verzija';
import { Modal } from './ui/Modal';

export const NAZIV_APLIKACIJE = 'TPM pregledi';

interface OAplikacijiProps {
  open: boolean;
  onClose: () => void;
}

/**
 * Dijalog „O aplikaciji" (blueprint 11 §4): verzija frontenda (build-time) i
 * servera (`GET /version`). Kad se razlikuju, korisnik drži keširani stari
 * bundle posle deploy-a → žuto upozorenje da osveži (Ctrl+F5).
 */
export function OAplikaciji({ open, onClose }: OAplikacijiProps) {
  const upit = useQuery({
    queryKey: ['version'],
    queryFn: async () => (await api.get<VersionDto>('/version')).data,
    enabled: open,
    staleTime: 5 * 60_000,
  });
  const server = upit.data;
  const verzijeSeRazlikuju = server !== undefined && server.verzija !== VERZIJA;

  const serverVrednost = (v: string | null | undefined) =>
    upit.isPending ? 'Učitavanje…' : upit.isError ? 'nedostupno' : (v ?? '—');

  return (
    <Modal open={open} title="O aplikaciji" onClose={onClose}>
      <div className="space-y-4">
        <dl className="divide-y divide-gray-100 text-sm">
          <Red naziv="Naziv aplikacije" vrednost={NAZIV_APLIKACIJE} />
          <Red naziv="Verzija aplikacije" vrednost={VERZIJA} />
          <Red naziv="Verzija servera" vrednost={serverVrednost(server?.verzija)} />
          <Red
            naziv="Build"
            vrednost={serverVrednost(server?.buildUtc ? formatirajVremeUtc(server.buildUtc) : null)}
          />
          <Red naziv="Okruženje" vrednost={serverVrednost(server?.okruzenje)} />
          <Red naziv="Git SHA (frontend)" vrednost={<code>{GIT_SHA}</code>} />
          <Red naziv="Git SHA (server)" vrednost={<code>{serverVrednost(server?.gitSha)}</code>} />
        </dl>

        {verzijeSeRazlikuju && (
          <div
            role="alert"
            className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800"
          >
            <AlertTriangle size={16} className="mt-0.5 shrink-0" aria-hidden />
            <span>Učitana je starija verzija. Osvežite stranicu (Ctrl+F5).</span>
          </div>
        )}
      </div>
    </Modal>
  );
}

function Red({ naziv, vrednost }: { naziv: string; vrednost: React.ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-4 py-2">
      <dt className="text-gray-500">{naziv}</dt>
      <dd className="text-right font-medium text-gray-800">{vrednost}</dd>
    </div>
  );
}
