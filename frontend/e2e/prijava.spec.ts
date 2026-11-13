import { expect, test } from '@playwright/test';
import { lozinka, NALOZI } from './podaci';

test('prijava: pogrešna lozinka javlja grešku, ispravna vodi na Početnu', async ({ page }) => {
  await page.goto('/kalendar');
  await expect(page).toHaveURL(/\/login/);

  await page.getByLabel('Korisničko ime').fill(NALOZI.direktor.ime);
  await page.getByLabel('Lozinka', { exact: true }).fill('pogresna-lozinka');
  await page.getByRole('button', { name: 'Prijavi se' }).click();
  await expect(page.getByRole('alert').first()).toBeVisible();
  await expect(page).toHaveURL(/\/login/);

  await page.getByLabel('Lozinka', { exact: true }).fill(lozinka());
  await page.getByRole('button', { name: 'Prijavi se' }).click();
  // Posle prijave korisnik ide tamo gde je krenuo (Kalendar).
  await expect(page).toHaveURL(/\/kalendar/);
});
