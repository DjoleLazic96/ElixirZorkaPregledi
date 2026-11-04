import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type {
  AktivnostDto,
  AktivnostInput,
  DodatnoPoljeDto,
  SablonPregledDto,
  SablonVerzijaDto,
  SekcijaDto,
  UredjajDto,
  UredjajInput,
  UvozRezultatDto,
} from '../../api/types';

/**
 * Hookovi za šablone, registar opreme, katalog aktivnosti i dodatna polja. Uvoz i izmene registra
 * menjaju i ono što prikazuju drugi upiti (npr. „u kojim šablonima“), pa invalidiraju sve ključeve ove celine.
 */
const KEYS = ['sabloni', 'uredjaji', 'aktivnosti', 'dodatna-polja'] as const;

function useInvalidirajSve() {
  const qc = useQueryClient();
  return () => KEYS.forEach((k) => void qc.invalidateQueries({ queryKey: [k] }));
}

export function useSekcije() {
  return useQuery({
    queryKey: ['sekcije'],
    queryFn: async () => (await api.get<SekcijaDto[]>('/sekcije')).data,
    staleTime: Infinity,
  });
}

export function useSabloni() {
  return useQuery({
    queryKey: ['sabloni'],
    queryFn: async () => (await api.get<SablonPregledDto[]>('/sabloni')).data,
  });
}

export function useSablonVerzija(tip: string | null) {
  return useQuery({
    queryKey: ['sabloni', tip],
    queryFn: async () => (await api.get<SablonVerzijaDto>(`/sabloni/${tip}`)).data,
    enabled: tip !== null,
  });
}

export function useUvoziSablone() {
  const invalidiraj = useInvalidirajSve();
  return useMutation({
    mutationFn: async (fajl: File) => {
      const telo = new FormData();
      telo.append('fajl', fajl);
      return (await api.post<UvozRezultatDto>('/sabloni/uvoz', telo)).data;
    },
    onSuccess: invalidiraj,
  });
}

export interface UredjajiFilteri {
  sekcijaId: number | null;
  status: string | null;
  pretraga: string;
  bezSablona: boolean;
}

export function useUredjaji(f: UredjajiFilteri) {
  return useQuery({
    queryKey: ['uredjaji', f],
    queryFn: async () =>
      (
        await api.get<UredjajDto[]>('/uredjaji', {
          params: {
            sekcijaId: f.sekcijaId ?? undefined,
            status: f.status ?? undefined,
            pretraga: f.pretraga || undefined,
            bezSablona: f.bezSablona || undefined,
          },
        })
      ).data,
  });
}

export function useSacuvajUredjaj() {
  const invalidiraj = useInvalidirajSve();
  return useMutation({
    mutationFn: async (args: { id: number | null; input: UredjajInput }) =>
      args.id === null
        ? (await api.post<UredjajDto>('/uredjaji', args.input)).data
        : (await api.put<UredjajDto>(`/uredjaji/${args.id}`, args.input)).data,
    onSuccess: invalidiraj,
  });
}

export function usePromeniStatusUredjaja() {
  const invalidiraj = useInvalidirajSve();
  return useMutation({
    mutationFn: async (u: UredjajDto) =>
      (await api.post<UredjajDto>(`/uredjaji/${u.id}/${u.status === 'aktivan' ? 'van-upotrebe' : 'u-upotrebi'}`)).data,
    onSuccess: invalidiraj,
  });
}

export function useAktivnosti() {
  return useQuery({
    queryKey: ['aktivnosti'],
    queryFn: async () => (await api.get<AktivnostDto[]>('/aktivnosti')).data,
  });
}

export function useSacuvajAktivnost() {
  const invalidiraj = useInvalidirajSve();
  return useMutation({
    mutationFn: async (args: { id: number | null; input: AktivnostInput }) =>
      args.id === null
        ? (await api.post<AktivnostDto>('/aktivnosti', args.input)).data
        : (await api.put<AktivnostDto>(`/aktivnosti/${args.id}`, args.input)).data,
    onSuccess: invalidiraj,
  });
}

export function useDodatnaPolja() {
  return useQuery({
    queryKey: ['dodatna-polja'],
    queryFn: async () => (await api.get<DodatnoPoljeDto[]>('/dodatna-polja')).data,
  });
}
