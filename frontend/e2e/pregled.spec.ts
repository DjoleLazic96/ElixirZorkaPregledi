/// <reference lib="dom" />
import { readFileSync } from 'node:fs';
import { expect, test } from '@playwright/test';
import { PREGLED, stanje } from './podaci';

const podaci = () => JSON.parse(readFileSync(PREGLED, 'utf8')) as { id: number; zaOtkazivanje: number };
const pregledId = () => podaci().id;

test.describe.configure({ mode: 'serial' });

test.describe('Inženjer na telefonu', () => {
  test.use({ storageState: stanje('inzenjer') });

  test('započne PZ, popuni ga i preda bez interneta, a kad se veza vrati sve se pošalje', async ({ page, context }) => {
    await page.goto('/');
    await expect(page.getByText(/PZ\d-\d{4}-\d{4}/).first()).toBeVisible();
    // Inženjer ne vidi raspored: ukucana adresa ga vraća na Početnu.
    await page.goto('/raspored');
    await expect(page).not.toHaveURL(/\/raspored/);

    await page.goto(`/pregled/${pregledId()}/popuni`);
    await page.getByRole('button', { name: 'Započni pregled' }).click();
    await expect(page.getByRole('tab').first()).toBeVisible();

    await context.setOffline(true);
    await expect(page.getByRole('status').filter({ hasText: 'Bez interneta' })).toBeVisible();

    const tabovi = page.getByRole('tab');
    const n = await tabovi.count();
    for (let i = 0; i < n; i++) {
      await tabovi.nth(i).click();
      await page.getByRole('button', { name: /Sve neoznačeno u sekciji je ispravno/ }).click();
    }
    await expect(page.getByText(/Čeka slanje: [1-9]\d* promena/)).toBeVisible();

    await page.getByRole('button', { name: /Predaj pregled/ }).click();
    // Rezime pre predaje (zamerka 35): ništa se ne predaje bez drugog klika.
    const rezime = page.getByRole('dialog', { name: /Predaja/ });
    await expect(rezime.getByText('Ispravnih stavki', { exact: true })).toBeVisible();
    await rezime.getByRole('button', { name: 'Predaj', exact: true }).click();
    // Bez veze inženjer ostaje na pregledu (Kalendar bez interneta ne radi) — ručno testiranje 14.11.
    await expect(page.getByText(/Predato – čeka slanje/).first()).toBeVisible();
    await expect(page).toHaveURL(/\/pregled\/\d+\/popuni/);

    await context.setOffline(false);
    // Posle povratka veze red se isprazni, a pregled je na serveru predat.
    await expect(async () => {
      await page.goto(`/pregled/${pregledId()}/popuni`);
      await expect(page.getByText('Pregled je predat; menja se samo kroz doradu.')).toBeVisible({ timeout: 3_000 });
    }).toPass({ timeout: 45_000 });
    const red = await page.evaluate(
      () =>
        new Promise<number>((res) => {
          const o = indexedDB.open('tpm-pregledi');
          o.onsuccess = () => {
            const c = o.result.transaction('red').objectStore('red').count();
            c.onsuccess = () => res(c.result);
          };
        }),
    );
    expect(red).toBe(0);
  });
});

test.describe('Supervizor', () => {
  test.use({ storageState: stanje('supervizor') });

  test('vidi predat pregled i može da ga overi, a planiran PZ otkazuje uz razlog', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('navigation').first().getByRole('link', { name: 'Raspored' })).toBeVisible();
    await page.goto(`/pregledi/${pregledId()}`);
    await expect(page.getByRole('button', { name: /Overi/ }).first()).toBeVisible();

    await page.goto(`/pregledi/${podaci().zaOtkazivanje}`);
    await page.getByRole('button', { name: 'Otkaži pregled' }).click();
    const dijalog = page.getByRole('dialog', { name: /Otkaži pregled/ });
    await dijalog.getByLabel('Razlog').fill('E2E: zastoj otkazan');
    await dijalog.getByRole('button', { name: 'Otkaži pregled' }).click();
    await expect(page.getByText('Poništen').first()).toBeVisible();
  });
});
