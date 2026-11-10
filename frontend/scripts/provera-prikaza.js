/*
 * Provera prikaza (spec §8.1, R10.9): za svaku rutu proverava da stranica nema bočno pomeranje i da ništa ne izlazi iz ekrana,
 * kartice ili dijaloga, a na telefonu i da su dugmad najmanje 44 × 44 px. Pokreće se u konzoli pregledača (DevTools) na prijavljenoj aplikaciji, za svaku rolu i širinu
 * (375, 768, 1366, 1920 px; DevTools → Toggle device toolbar), sa internetom i bez njega:
 *
 *   await proveriPrikaz(['/', '/kalendar', '/pregledi', ...])
 *
 * Vraća spisak problema po ruti (prazno = sve u redu). Elementi u kontejneru sa sopstvenim pomeranjem (tabela u `overflow-x-auto`,
 * spisak u dijalogu) se ne broje kao izlazak iz okvira, ali se tabela koja se pomera bočno na ≥ 1366 px prijavljuje (§8.1 t. 4).
 */
window.proveriPrikaz = async function proveriPrikaz(rute, { cekanje = 1500 } = {}) {
  const spi = (ms) => new Promise((r) => setTimeout(r, ms));
  const vidljiv = (el) => {
    const s = getComputedStyle(el);
    const r = el.getBoundingClientRect();
    return s.display !== 'none' && s.visibility !== 'hidden' && r.width > 0 && r.height > 0;
  };
  const uPomeranju = (el) => {
    // Radni deo (main) se ne računa: njegovo bočno pomeranje je upravo greška koju tražimo.
    for (let p = el.parentElement; p && p.tagName !== 'MAIN' && p !== document.body; p = p.parentElement) {
      const s = getComputedStyle(p);
      if (/(auto|scroll|hidden)/.test(s.overflowX) && p.scrollWidth > p.clientWidth + 1) return true;
    }
    return false;
  };
  const opis = (el) => `${el.tagName.toLowerCase()}${el.getAttribute('aria-label') ? `[${el.getAttribute('aria-label')}]` : ''} „${(el.textContent || '').trim().slice(0, 40)}“`;

  const rezultat = {};
  for (const ruta of rute) {
    history.pushState({}, '', ruta);
    dispatchEvent(new PopStateEvent('popstate'));
    await spi(cekanje);
    for (let i = 0; i < 20 && document.body.innerText.includes('Učitavanje…'); i++) await spi(250);

    const problemi = [];
    const sirina = document.documentElement.clientWidth;
    if (document.documentElement.scrollWidth > sirina + 1) problemi.push(`stranica se pomera bočno (${document.documentElement.scrollWidth} > ${sirina})`);
    const main = document.querySelector('main');
    if (main && main.scrollWidth > main.clientWidth + 1) problemi.push(`radni deo se pomera bočno (${main.scrollWidth} > ${main.clientWidth})`);

    const okviri = [...document.querySelectorAll('main .rounded-lg.border, [role="dialog"]')];
    for (const el of document.querySelectorAll('main *, header *, [role="dialog"] *')) {
      if (!vidljiv(el) || el.children.length > 0 && el.tagName !== 'BUTTON' && el.tagName !== 'A') continue;
      const r = el.getBoundingClientRect();
      if ((r.right > sirina + 1 || r.left < -1) && !uPomeranju(el)) problemi.push(`van ekrana: ${opis(el)}`);
      const okvir = okviri.find((o) => o.contains(el) && o !== el);
      if (okvir) {
        const o = okvir.getBoundingClientRect();
        if ((r.right > o.right + 1 || r.left < o.left - 1) && !uPomeranju(el)) problemi.push(`van okvira: ${opis(el)}`);
      }
    }
    // Na telefonu dugmad najmanje 44 × 44 px (§8.1 t. 6).
    if (sirina < 768)
      for (const b of document.querySelectorAll('main button, header button, [role="dialog"] button'))
        if (vidljiv(b)) {
          const r = b.getBoundingClientRect();
          if (r.width < 43.5 || r.height < 43.5) problemi.push(`malo dugme ${Math.round(r.width)}×${Math.round(r.height)}: ${opis(b)}`);
        }
    if (sirina >= 1366)
      for (const t of document.querySelectorAll('main table'))
        if (t.parentElement && t.parentElement.scrollWidth > t.parentElement.clientWidth + 1) problemi.push(`tabela se pomera bočno na ${sirina} px`);
    if (problemi.length) rezultat[location.pathname] = [...new Set(problemi)].slice(0, 12);
  }
  return rezultat;
};
