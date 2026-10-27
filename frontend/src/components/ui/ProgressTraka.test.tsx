import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ProgressTraka } from './ProgressTraka';

function ton(percent: number | null, inverted = false) {
  const pragovi = inverted ? { warnAt: 20, dangerAt: 10 } : { warnAt: 70, dangerAt: 90 };
  const { unmount } = render(
    <ProgressTraka percent={percent} {...pragovi} inverted={inverted} aria-label="Traka" />,
  );
  const t = screen.getByRole('progressbar', { name: 'Traka' }).getAttribute('data-ton');
  unmount();
  return t;
}

describe('ProgressTraka', () => {
  it('više je lošije (CPU 70/90): ispod žutog praga zeleno, od žutog žuto, od crvenog crveno', () => {
    expect(ton(69.9)).toBe('ok');
    expect(ton(70)).toBe('upozorenje');
    expect(ton(89)).toBe('upozorenje');
    expect(ton(90)).toBe('kriticno');
  });

  it('inverted — manje je lošije (slobodan disk 20/10)', () => {
    expect(ton(25, true)).toBe('ok');
    expect(ton(20, true)).toBe('upozorenje');
    expect(ton(10, true)).toBe('upozorenje');
    expect(ton(9.9, true)).toBe('kriticno');
  });

  it('null vrednost daje praznu sivu traku sa tekstom „nedostupno"', () => {
    render(<ProgressTraka percent={null} warnAt={70} dangerAt={90} aria-label="CPU" />);
    const traka = screen.getByRole('progressbar', { name: 'CPU' });
    expect(traka).toHaveAttribute('data-ton', 'nepoznato');
    expect(traka).toHaveAttribute('aria-valuetext', 'nedostupno');
    expect(traka).not.toHaveAttribute('aria-valuenow');
  });

  it('izlaže vrednost kroz aria-valuenow i seče širinu na 0–100', () => {
    render(<ProgressTraka percent={150} warnAt={70} dangerAt={90} aria-label="RAM" />);
    const traka = screen.getByRole('progressbar', { name: 'RAM' });
    expect(traka).toHaveAttribute('aria-valuenow', '150');
    expect(traka.firstElementChild).toHaveStyle({ width: '100%' });
  });
});
