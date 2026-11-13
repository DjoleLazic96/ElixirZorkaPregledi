/**
 * Smanjivanje slike na telefonu pre slanja (R4.5, spec §5.1): JPEG, duža strana najviše 2048 px, cilj 1–2 MB.
 * Ako pregledač ne ume da dekodira fajl, vraća original (server ga i dalje ograničava na 3 MB).
 */
const NAJDUZA_STRANA = 2048;

/** Nove dimenzije koje čuvaju odnos stranica, a duža strana nije veća od `najvise`. */
export function smanjeneDimenzije(sirina: number, visina: number, najvise = NAJDUZA_STRANA): { sirina: number; visina: number } {
  const duza = Math.max(sirina, visina);
  if (duza <= najvise) return { sirina, visina };
  const k = najvise / duza;
  return { sirina: Math.round(sirina * k), visina: Math.round(visina * k) };
}

export async function smanjiSliku(fajl: File): Promise<Blob> {
  try {
    const bitmap = await createImageBitmap(fajl);
    const { sirina, visina } = smanjeneDimenzije(bitmap.width, bitmap.height);
    const canvas = document.createElement('canvas');
    canvas.width = sirina;
    canvas.height = visina;
    canvas.getContext('2d')?.drawImage(bitmap, 0, 0, sirina, visina);
    bitmap.close();
    for (const kvalitet of [0.85, 0.7, 0.55]) {
      const blob = await new Promise<Blob | null>((r) => canvas.toBlob(r, 'image/jpeg', kvalitet));
      if (blob && blob.size <= 2 * 1024 * 1024) return blob;
    }
    return (await new Promise<Blob | null>((r) => canvas.toBlob(r, 'image/jpeg', 0.45))) ?? fajl;
  } catch {
    return fajl;
  }
}
