import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { PersianDateField, PersianDateTimeField } from './PersianDateTime';

// `(1404).toLocaleString('fa-IR')` groups four-digit numbers by default ("۱٬۴۰۴"), which is a
// thousands separator, not something a calendar year should ever show.
describe('PersianDateTime year options', () => {
  it('renders year options with no thousands separator', () => {
    render(<PersianDateField value="" onChange={() => {}} />);
    const year = screen.getByLabelText('سال') as HTMLSelectElement;
    for (const option of Array.from(year.options)) {
      expect(option.textContent).not.toContain('٬');
    }
  });

  it('renders the same clean years in the date-time variant', () => {
    render(<PersianDateTimeField value="" onChange={() => {}} />);
    const year = screen.getByLabelText('سال') as HTMLSelectElement;
    for (const option of Array.from(year.options)) {
      expect(option.textContent).not.toContain('٬');
    }
  });
});
