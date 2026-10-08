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

/** `YYYY-MM-DD` from a date-input -> ISO timestamp (midnight UTC, as the add form has always sent). */
export function dateInputToIso(value: string): string | null {
  return value ? new Date(value).toISOString() : null;
}

export function isoToDateInput(iso: string | null): string {
  return iso ? iso.slice(0, 10) : "";
}

/** Local calendar date `today + days` as `YYYY-MM-DD`. */
export function localDatePlusDays(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() + days);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
