import { api } from '../api/client';

/**
 * Preuzimanje fajlova (Excel i sl.) sa backenda kao blob (blueprint 13 §8).
 * Fajl generiše server; mi samo skidamo binarni odgovor i pokrećemo download.
 */

/**
 * Ime fajla iz `Content-Disposition` headera. Prvo RFC 5987 (`filename*=UTF-8''x`),
 * pa običan `filename=` (quoted ili unquoted, kako ASP.NET šalje).
 */
export function imeFajlaIzDispozicije(
  header: string | null | undefined,
  fallback = 'preuzimanje.bin',
): string {
  if (!header) return fallback;
  const star = /filename\*=(?:UTF-8'')?"?([^";]+)"?/i.exec(header);
  if (star) {
    try {
      return decodeURIComponent(star[1].trim());
    } catch {
      // Neispravno enkodirano ime — padni na običan `filename=`.
    }
  }
  const plain = /filename="?([^";]+)"?/i.exec(header);
  return plain ? plain[1].trim() : fallback;
}

/** Sačuvaj blob kao fajl (privremeni `<a download>`; URL se oslobađa odloženo zbog Safari-ja). */
export function sacuvajBlob(blob: Blob, ime: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = ime;
  document.body.appendChild(a);
  a.click();
  a.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1_000);
}

/**
 * `GET url` sa dugim timeout-om (izvoz može da traje) i pokretanje download-a.
 * Dugme koje ovo zove prikazuje „Preuzimanje…" dok Promise traje.
 */
export async function preuzmiFajl(
  url: string,
  params: Record<string, string | number | boolean | undefined>,
  fallbackIme: string,
): Promise<void> {
  const r = await api.get<Blob>(url, { params, responseType: 'blob', timeout: 120_000 });
  const ime = imeFajlaIzDispozicije(
    r.headers['content-disposition'] as string | undefined,
    fallbackIme,
  );
  sacuvajBlob(r.data, ime);
}
