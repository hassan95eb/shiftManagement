import { useEffect, useMemo, useRef, useState } from 'react';
import { JALAALI_MONTH_NAMES, jalaaliMonthLength, toGregorian, toJalaali } from '../lib/jalali';

function pad2(value: number) {
  return value.toLocaleString('fa-IR', { minimumIntegerDigits: 2 });
}

function todayJalaali() {
  const now = new Date();
  return toJalaali(now.getFullYear(), now.getMonth() + 1, now.getDate());
}

function useJalaaliYearOptions() {
  return useMemo(() => {
    const { jy } = todayJalaali();
    return Array.from({ length: 7 }, (_, index) => jy - 1 + index);
  }, []);
}

interface DateParts {
  jy?: number;
  jm?: number;
  jd?: number;
}

function parseDateOnly(value: string): DateParts {
  if (!value) return {};
  const [gy, gm, gd] = value.split('-').map(Number);
  if (!gy || !gm || !gd) return {};
  return toJalaali(gy, gm, gd);
}

interface DateTimeParts extends DateParts {
  hh?: number;
  mi?: number;
}

function parseDateTime(value: string): DateTimeParts {
  if (!value) return {};
  const [datePart, timePart] = value.split('T');
  const dateParts = parseDateOnly(datePart);
  if (!timePart) return dateParts;
  const [hh, mi] = timePart.split(':').map(Number);
  return { ...dateParts, hh: Number.isNaN(hh) ? undefined : hh, mi: Number.isNaN(mi) ? undefined : mi };
}

/**
 * The field is built from independent day/month/(year/hour/minute) <select>s, but the value
 * it exchanges with the parent is a single all-or-nothing string (Gregorian `YYYY-MM-DD[THH:mm]`,
 * empty when incomplete). A selection made while some other part is still unset has nothing
 * representable to report upward, so intermediate progress is kept in local state — seeded from
 * `value` on mount and re-synced only when `value` changes for a reason other than our own last
 * `onChange` call (i.e. the parent reset the form, not an echo of what we just sent it).
 */
function usePartsState<Parts extends DateParts>(value: string, onChange: (value: string) => void, parse: (value: string) => Parts, format: (parts: Parts) => string | undefined) {
  const [parts, setParts] = useState<Parts>(() => parse(value));
  const lastEmitted = useRef(value);

  useEffect(() => {
    if (value !== lastEmitted.current) {
      lastEmitted.current = value;
      setParts(parse(value));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value]);

  function update(next: Partial<Parts>) {
    const merged = { ...parts, ...next };
    setParts(merged);
    const formatted = format(merged) ?? '';
    lastEmitted.current = formatted;
    onChange(formatted);
  }

  return { parts, update };
}

function formatDateOnly({ jy, jm, jd }: DateParts): string | undefined {
  if (jy == null || jm == null || jd == null) return undefined;
  const { gy, gm, gd } = toGregorian(jy, jm, jd);
  return `${gy}-${String(gm).padStart(2, '0')}-${String(gd).padStart(2, '0')}`;
}

function formatDateTime(value: DateTimeParts): string | undefined {
  const datePart = formatDateOnly(value);
  if (!datePart || value.hh == null || value.mi == null) return undefined;
  return `${datePart}T${String(value.hh).padStart(2, '0')}:${String(value.mi).padStart(2, '0')}`;
}

function DaySelect({ jy, jm, jd, onChange }: { jy?: number; jm?: number; jd?: number; onChange: (day: number | undefined) => void }) {
  const dayCount = jy && jm ? jalaaliMonthLength(jy, jm) : 31;
  const days = Array.from({ length: dayCount }, (_, index) => index + 1);
  return <select aria-label="روز" value={jd ?? ''} onChange={(event) => onChange(event.target.value ? Number(event.target.value) : undefined)}>
    <option value="">روز</option>
    {days.map((day) => <option key={day} value={day}>{pad2(day)}</option>)}
  </select>;
}

function MonthSelect({ jm, onChange }: { jm?: number; onChange: (month: number | undefined) => void }) {
  return <select aria-label="ماه" value={jm ?? ''} onChange={(event) => onChange(event.target.value ? Number(event.target.value) : undefined)}>
    <option value="">ماه</option>
    {JALAALI_MONTH_NAMES.map((name, index) => <option key={name} value={index + 1}>{name}</option>)}
  </select>;
}

function YearSelect({ jy, onChange }: { jy?: number; onChange: (year: number | undefined) => void }) {
  const years = useJalaaliYearOptions();
  return <select aria-label="سال" value={jy ?? ''} onChange={(event) => onChange(event.target.value ? Number(event.target.value) : undefined)}>
    <option value="">سال</option>
    {years.map((year) => <option key={year} value={year}>{year.toLocaleString('fa-IR', { useGrouping: false })}</option>)}
  </select>;
}

/** Persian-calendar replacement for `<input type="date">`. Value/onChange stay Gregorian `YYYY-MM-DD`. */
export function PersianDateField({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const { parts, update } = usePartsState(value, onChange, parseDateOnly, formatDateOnly);

  return <div className="persian-date">
    <DaySelect jy={parts.jy} jm={parts.jm} jd={parts.jd} onChange={(day) => update({ jd: day })} />
    <MonthSelect jm={parts.jm} onChange={(month) => update({ jm: month, jd: undefined } as Partial<DateParts>)} />
    <YearSelect jy={parts.jy} onChange={(year) => update({ jy: year })} />
  </div>;
}

/** Persian-calendar replacement for `<input type="datetime-local">`. Value/onChange stay Gregorian `YYYY-MM-DDTHH:mm`. */
export function PersianDateTimeField({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const { parts, update } = usePartsState(value, onChange, parseDateTime, formatDateTime);
  const hours = Array.from({ length: 24 }, (_, index) => index);
  const minutes = Array.from({ length: 60 }, (_, index) => index);

  return <div className="persian-date persian-date--time">
    <DaySelect jy={parts.jy} jm={parts.jm} jd={parts.jd} onChange={(day) => update({ jd: day })} />
    <MonthSelect jm={parts.jm} onChange={(month) => update({ jm: month, jd: undefined } as Partial<DateTimeParts>)} />
    <YearSelect jy={parts.jy} onChange={(year) => update({ jy: year })} />
    <select aria-label="ساعت" value={parts.hh ?? ''} onChange={(event) => update({ hh: event.target.value === '' ? undefined : Number(event.target.value) })}>
      <option value="">ساعت</option>
      {hours.map((hour) => <option key={hour} value={hour}>{pad2(hour)}</option>)}
    </select>
    <select aria-label="دقیقه" value={parts.mi ?? ''} onChange={(event) => update({ mi: event.target.value === '' ? undefined : Number(event.target.value) })}>
      <option value="">دقیقه</option>
      {minutes.map((minute) => <option key={minute} value={minute}>{pad2(minute)}</option>)}
    </select>
  </div>;
}
