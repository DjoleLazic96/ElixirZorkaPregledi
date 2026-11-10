/** Vrste i hitnosti akcija (R7.1, R7.2) — ista značenja kao `AkcijaPravila` na serveru. */
export const VRSTE_AKCIJA: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'ocistiti', label: 'Očistiti' },
  { value: 'podmazati', label: 'Podmazati ili dopuniti' },
  { value: 'zameniti', label: 'Zameniti' },
  { value: 'popraviti', label: 'Popraviti ili sanirati' },
  { value: 'ostalo', label: 'Ostalo' },
];

export const HITNOSTI: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'hitno', label: 'Hitno' },
  { value: '7_dana', label: 'U narednih 7 dana' },
  { value: 'sledeci_pz', label: 'U sledećem planskom zastoju' },
  { value: 'pratiti', label: 'Pratiti' },
];

export const ZADUZENI_PREDLOZI = ['Održavanje', 'Elektro', 'Mašinska', 'Spoljni izvođač'];

export const vrstaLabela = (v: string) => VRSTE_AKCIJA.find((x) => x.value === v)?.label ?? v;
export const hitnostLabela = (v: string) => HITNOSTI.find((x) => x.value === v)?.label ?? v;

/** Boja hitnosti po mockup-u: Hitno crveno, 7 dana narandžasto, sledeći PZ plavo, Pratiti sivo. */
export const tonHitnosti = (h: string) => (h === 'hitno' ? 'red' : h === '7_dana' ? 'orange' : h === 'sledeci_pz' ? 'blue' : 'gray') as 'red' | 'orange' | 'blue' | 'gray';
