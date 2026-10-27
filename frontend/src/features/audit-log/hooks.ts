import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { AuditEntitetDto, AuditFilteri, AuditRedDto, StranicaDto } from '../../api/types';

const KEY = 'audit';

/**
 * `GET /audit` sa filterima i paginacijom. `keepPreviousData` zadržava prethodnu
 * stranicu dok se nova učitava (bez treptaja). Prazni filteri se ne šalju;
 * `doDatum` ide na žicu kao `do` (rezervisana reč u JS-u).
 */
export function useAudit(filteri: AuditFilteri) {
  return useQuery({
    queryKey: [KEY, filteri],
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const params: Record<string, string | number> = {
        stranica: filteri.stranica,
        velicina: filteri.velicina,
      };
      if (filteri.korisnik) params.korisnik = filteri.korisnik;
      if (filteri.entitet) params.entitet = filteri.entitet;
      if (filteri.akcija) params.akcija = filteri.akcija;
      if (filteri.od) params.od = filteri.od;
      if (filteri.doDatum) params.do = filteri.doDatum;
      return (await api.get<StranicaDto<AuditRedDto>>('/audit', { params })).data;
    },
  });
}

/** Lista entiteta za filter — šifarnik, retko se menja (5 min staleTime). */
export function useAuditEntiteti() {
  return useQuery({
    queryKey: [KEY, 'entiteti'],
    queryFn: async () => (await api.get<AuditEntitetDto[]>('/audit/entiteti')).data,
    staleTime: 5 * 60_000,
  });
}
