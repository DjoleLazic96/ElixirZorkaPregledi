import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';

// Posle svakog testa odmontiraj sve renderovane komponente da test-ovi ne
// cure DOM jedan u drugi. Bez `globals: true` ovaj cleanup ne ide automatski.
afterEach(() => {
  cleanup();
});
