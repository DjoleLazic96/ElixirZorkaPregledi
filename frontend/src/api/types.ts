/**
 * Ručno pisani DTO tipovi — ogledalo backend DTO-a (ista imena polja, camelCase).
 * Ovo je jedina tačka usklađivanja sa backendom: feature-i uvoze odavde, ne
 * definišu sopstvene oblike. Nema generatora koda (generatori daju nevažeći TS
 * za query parametar `do`), pa se izmena na backendu ručno preslikava ovde.
 */
import type { Rola } from '../lib/permissions';

// ─── Auth (tehničke rute, engleska polja — identične u svim aplikacijama) ──
export interface UserDto {
  id: string;
  userName: string;
  fullName: string;
  email: string | null;
}

export interface LoginResponse {
  accessToken: string;
  /** ISO trenutak isteka access tokena. */
  expiresAt: string;
  user: UserDto;
  roles: string[];
}

export interface CurrentUserResponse {
  user: UserDto;
  roles: string[];
}

/** `POST /auth/change-password` → 204; 400 vraća `errors.currentPassword|newPassword`. */
export interface PromenaLozinkeInput {
  currentPassword: string;
  newPassword: string;
}

// ─── Verzija (`GET /version`, anonimno) ───────────────────────────────────
export interface VersionDto {
  verzija: string;
  gitSha: string;
  /** ISO UTC trenutak build-a ili null kad build nije prosledio vrednost. */
  buildUtc: string | null;
  okruzenje: string;
}

// ─── Paginacija (`StranicaDto<T>` na backendu) ────────────────────────────
export interface StranicaDto<T> {
  stavke: T[];
  ukupno: number;
  stranica: number;
  velicina: number;
}

// ─── Greške — RFC 7807 problem+json ───────────────────────────────────────
export interface ApiProblem {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  /** W3C trace id (32 hex) — ključ za pretragu loga kod 500. */
  traceId?: string;
  /** 400: FluentValidation mapa polje → poruke. */
  errors?: Record<string, string[]>;
  /** 422: poslovna pravila — gotove rečenice za prikaz. */
  greske?: string[];
}

// ─── Korisnici (`/korisnici`, Admin) ──────────────────────────────────────
export interface KorisnikDto {
  id: string;
  korisnickoIme: string;
  ime: string;
  email: string | null;
  rola: Rola;
  aktivan: boolean;
  /** ISO ili null ako se korisnik nikad nije prijavio. */
  poslednjaPrijava: string | null;
}

/** `POST /korisnici` */
export interface KorisnikInput {
  korisnickoIme: string;
  ime: string;
  email?: string | null;
  lozinka: string;
  rola: Rola;
}

/** `PUT /korisnici/{id}` — korisničko ime je nepromenljivo. */
export interface KorisnikIzmenaInput {
  ime: string;
  email?: string | null;
  rola: Rola;
}

/** `POST /korisnici/{id}/reset-lozinka` → 204 */
export interface ResetLozinkaInput {
  novaLozinka: string;
}

// ─── Audit log (`/audit`, Admin) ──────────────────────────────────────────
export type AuditAkcija =
  | 'CREATE'
  | 'UPDATE'
  | 'DELETE'
  | 'LOGIN'
  | 'LOGIN_FAIL'
  | 'LOGOUT'
  | 'START';

export interface AuditRedDto {
  id: number;
  /** ISO UTC. */
  vreme: string;
  korisnik: string | null;
  akcija: AuditAkcija;
  /** CLR ime entiteta (`Kategorija`, `ApplicationUser`, `Auth`, `Sistem`). */
  entitet: string;
  /** Srpska labela sa backenda (`EntitetLabele`). */
  entitetLabela: string;
  entitetId: string | null;
  ip: string | null;
  /** Sirovi JSON string snapshot-a ili null; `DiffPrikaz` ga parsira. */
  stareVrednosti: string | null;
  noveVrednosti: string | null;
}

/** `GET /audit/entiteti` — lista za filter „Entitet". */
export interface AuditEntitetDto {
  entitet: string;
  labela: string;
}

/**
 * Filteri audit loga. HTTP query param se zove `do` (rezervisana reč u JS-u),
 * pa polje ovde nosi ime `doDatum`, a hook ga šalje kao `do`. `od`/`doDatum`
 * su UTC ISO granice dana (vidi `lib/datum.ts` `pocetakDanaUtc`/`krajDanaUtc`).
 */
export interface AuditFilteri {
  stranica: number;
  velicina: number;
  korisnik?: string;
  entitet?: string;
  akcija?: AuditAkcija;
  od?: string;
  doDatum?: string;
}

// ─── Stanje servera (`/stanje-servera`, Admin) ────────────────────────────
export interface AplikacijaStanje {
  verzija: string;
  gitSha: string;
  dotnetVerzija: string;
  okruzenje: string;
  uptimeSekundi: number;
  /** ISO UTC — prikazuje se sa oznakom „(UTC)". */
  vremeUtc: string;
}

export interface BazaStanje {
  veza: boolean;
  postgresVerzija: string | null;
  velicinaBazeBytes: number | null;
  aktivneKonekcije: number | null;
  odzivMs: number | null;
  /** Najnoviji backup fajl; null kad folder ne postoji ili nije čitljiv. */
  poslednjiBackupUtc: string | null;
  /** Samo tip izuzetka (npr. `NpgsqlException`), nikad poruka. */
  greska: string | null;
}

export interface HostStanje {
  osOpis: string | null;
  cpuProcenat: number | null;
  ramUkupnoBytes: number | null;
  ramIskoriscenoBytes: number | null;
  diskUkupnoBytes: number | null;
  diskSlobodnoBytes: number | null;
}

export interface ServerStanje {
  aplikacija: AplikacijaStanje;
  baza: BazaStanje;
  /** null = metrike hosta se ne mogu pročitati. */
  host: HostStanje | null;
}

// ─── Kategorije — PRIMER feature-a (šifarnik sa paginacijom) ──────────────
export interface KategorijaDto {
  id: number;
  sifra: string;
  naziv: string;
  opis: string | null;
  aktivan: boolean;
  createdAt: string;
  updatedAt: string | null;
}

/** `POST /kategorije` i `PUT /kategorije/{id}` */
export interface KategorijaInput {
  sifra: string;
  naziv: string;
  opis: string | null;
}

export type KategorijaSort = 'sifra' | 'naziv';
export type Smer = 'asc' | 'desc';

export interface KategorijeFilteri {
  stranica: number;
  velicina: number;
  pretraga?: string;
  sort: KategorijaSort;
  smer: Smer;
  /** undefined = i aktivne i neaktivne. */
  aktivan?: boolean;
}
