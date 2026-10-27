import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { Button } from './Button';

describe('Button', () => {
  it('renderuje decu i okida onClick', () => {
    const onClick = vi.fn();
    render(<Button onClick={onClick}>Sačuvaj</Button>);
    const btn = screen.getByRole('button', { name: 'Sačuvaj' });
    // Default type mora biti "button" (a ne "submit") da dugme van forme ne
    // pokušava da submit-uje. Koristimo običan getAttribute umesto jest-dom
    // matchera da izbegnemo dodatni matcher paket.
    expect(btn.getAttribute('type')).toBe('button');
    fireEvent.click(btn);
    expect(onClick).toHaveBeenCalledOnce();
  });
});
