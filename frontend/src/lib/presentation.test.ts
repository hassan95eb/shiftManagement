import { describe, expect, it } from 'vitest';
import { asUtc, formatDuration, formatPersianTime } from './presentation';

// The API serializes entity timestamps (StartUtc, EndUtc, CreatedAtUtc, ...) without a `Z` —
// DateTimeKind is lost round-tripping a DateTime through EF Core / SQL Server — even though the
// value is a real UTC instant. `new Date(...)` treats a designator-less ISO string as *local*
// time, which is wrong for every viewer not in UTC (every real user here is in Tehran). These
// pin the fix: a bare timestamp must be treated exactly like its `Z`-suffixed equivalent.
describe('asUtc', () => {
  it('appends Z to a timestamp with no timezone designator', () => {
    expect(asUtc('2026-11-15T04:30:00')).toBe('2026-11-15T04:30:00Z');
  });

  it('leaves a Z-suffixed timestamp unchanged', () => {
    expect(asUtc('2026-11-15T04:30:00.000Z')).toBe('2026-11-15T04:30:00.000Z');
  });

  it('leaves a timestamp with an explicit numeric offset unchanged', () => {
    expect(asUtc('2026-11-15T08:00:00+03:30')).toBe('2026-11-15T08:00:00+03:30');
  });
});

describe('formatPersianTime with the API\'s designator-less timestamps', () => {
  it('reads a bare timestamp as UTC, same as its Z-suffixed equivalent', () => {
    expect(formatPersianTime('2026-11-15T04:30:00')).toBe(formatPersianTime('2026-11-15T04:30:00.000Z'));
  });

  it('renders the correct Tehran wall-clock time (UTC+03:30) for a bare timestamp', () => {
    // 04:30 UTC == 08:00 Tehran.
    expect(formatPersianTime('2026-11-15T04:30:00')).toBe('۰۸:۰۰');
  });
});

describe('formatDuration with the API\'s designator-less timestamps', () => {
  it('is unaffected by the missing Z (both ends shift by the same normalization)', () => {
    expect(formatDuration('2026-11-15T04:30:00', '2026-11-15T12:30:00')).toBe('۸ ساعت');
  });

  it('never inserts a thousands separator, even for a four-digit hour count', () => {
    // `(1234).toLocaleString('fa-IR')` groups by default ("۱٬۲۳۴") — wrong for a plain count.
    // A multi-year availability window (a real mistake a user can make while picking dates)
    // must still render as a plain number of hours.
    expect(formatDuration('2020-01-01T00:00:00', '2020-06-25T10:00:00')).not.toContain('٬');
  });
});
