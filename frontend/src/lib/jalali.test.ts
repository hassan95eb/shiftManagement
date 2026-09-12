import { describe, expect, it } from 'vitest';
import { isLeapJalaaliYear, jalaaliMonthLength, toGregorian, toJalaali } from './jalali';

// Every pair below was cross-checked against the platform's own Persian calendar
// (`new Intl.DateTimeFormat('en-US-u-ca-persian', { timeZone: 'UTC' })`), which is
// the authority this hand-written conversion has to agree with.
describe('toJalaali', () => {
  it.each([
    [2024, 3, 19, 1402, 12, 29],
    [2024, 3, 20, 1403, 1, 1],
    [2025, 3, 20, 1403, 12, 30],
    [2025, 3, 21, 1404, 1, 1],
    [2026, 3, 20, 1404, 12, 29],
    [2026, 3, 21, 1405, 1, 1],
    [2020, 3, 19, 1398, 12, 29],
    [2020, 3, 20, 1399, 1, 1],
    [2026, 9, 12, 1405, 6, 21],
  ])('%i-%i-%i -> %i-%i-%i', (gy, gm, gd, jy, jm, jd) => {
    expect(toJalaali(gy, gm, gd)).toEqual({ jy, jm, jd });
  });
});

describe('toGregorian', () => {
  it('round-trips every toJalaali fixture back to the original Gregorian date', () => {
    const fixtures: [number, number, number][] = [
      [2024, 3, 19], [2024, 3, 20], [2025, 3, 20], [2025, 3, 21],
      [2026, 3, 20], [2026, 3, 21], [2020, 3, 19], [2020, 3, 20], [2026, 9, 12],
    ];
    for (const [gy, gm, gd] of fixtures) {
      const { jy, jm, jd } = toJalaali(gy, gm, gd);
      expect(toGregorian(jy, jm, jd)).toEqual({ gy, gm, gd });
    }
  });
});

describe('jalaaliMonthLength', () => {
  it('is 31 for the first six months', () => {
    expect(jalaaliMonthLength(1403, 1)).toBe(31);
    expect(jalaaliMonthLength(1403, 6)).toBe(31);
  });

  it('is 30 for months 7-11', () => {
    expect(jalaaliMonthLength(1403, 7)).toBe(30);
    expect(jalaaliMonthLength(1403, 11)).toBe(30);
  });

  it('is 30 in Esfand of a leap year and 29 otherwise', () => {
    expect(isLeapJalaaliYear(1403)).toBe(true);
    expect(jalaaliMonthLength(1403, 12)).toBe(30);
    expect(isLeapJalaaliYear(1404)).toBe(false);
    expect(jalaaliMonthLength(1404, 12)).toBe(29);
  });
});
