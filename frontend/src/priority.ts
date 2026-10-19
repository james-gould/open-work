/**
 * Priority is stored as a number where higher = more important (the API sorts it
 * descending as the final tiebreaker). These values match the Daily Driver sweep's
 * severity mapping so items from both sources sort consistently.
 */
export const PRIORITY_OPTIONS = [
  { label: "Critical", value: 10 },
  { label: "Warning", value: 5 },
  { label: "Acceptable", value: 1 }
] as const;

export function priorityLabel(priority: number): string {
  if (priority >= 10) return "Critical";
  if (priority >= 5) return "Warning";
  return "Acceptable";
}

/** Snaps an arbitrary stored number onto the nearest dropdown option. */
export function priorityOptionValue(priority: number): number {
  if (priority >= 10) return 10;
  if (priority >= 5) return 5;
  return 1;
}

/** Local date-time input value -> ISO timestamp. */
export function dateTimeInputToIso(value: string): string | null {
  return value ? new Date(value).toISOString() : null;
}

/** ISO timestamp -> local `YYYY-MM-DDTHH:mm` value for a datetime-local input. */
export function isoToDateTimeInput(iso: string | null): string {
  if (!iso) return "";

  const date = new Date(iso);
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

/** Local date and time offset from now as `YYYY-MM-DDTHH:mm`. */
export function localDateTimePlus(days = 0, hours = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + days);
  d.setHours(d.getHours() + hours);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
