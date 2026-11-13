import { expect, test, type Page } from '@playwright/test';
import { stanje } from './podaci';

/** Stavke bočnog menija koje rola mora da vidi, odnosno ne sme da vidi (R1.5, `app/layout/nav.ts`). */
async function meni(page: Page, vidi: string[], neVidi: string[]) {
  const nav = page.getByRole('navigation').first();
  for (const s of vidi) await expect(nav.getByRole('link', { name: s }), `vidi „${s}“`).toBeVisible();
  for (const s of neVidi) await expect(nav.getByRole('link', { name: s }), `ne vidi „${s}“`).toHaveCount(0);
}

test.describe('Admin', () => {
  test.use({ storageState: stanje('admin') });
  test('meni i korisnici', async ({ page }) => {
    await page.goto('/');
    await meni(page, ['Administracija', 'Audit log', 'Stanje servera', 'Šabloni'], ['Moja odsustva', 'Raspored']);
    await page.goto('/admin/korisnici');
    await expect(page.getByText('provera.inzenjer').first()).toBeVisible();
    // Sekcije uređuje samo Admin (zamerka 33).
    await page.getByRole('tab', { name: 'Sekcije' }).click();
    await expect(page.getByRole('button', { name: 'Nova sekcija' })).toBeVisible();
  });
});

test.describe('Direktor', () => {
  test.use({ storageState: stanje('direktor') });
  test('meni i zaštita administracije', async ({ page }) => {
    await page.goto('/');
    await meni(page, ['Raspored', 'Analitika', 'Kalendar'], ['Administracija', 'Audit log', 'Moja odsustva']);
    // Ruta bez prava tiho vraća na Početnu (guard na svakoj ruti, blueprint 13 §4).
    await page.goto('/admin/korisnici');
    await expect(page).not.toHaveURL(/\/admin/);
  });
});
