/** What the backend allows (`GET /certifications/serial-limits`); these are the defaults until it answers. */
export interface SerialLimits {
  maxCharactersPerCertificate: number;
  maxLengthPerSerialNumber: number;
  maxSerialNumbers: number;
  /** A gear certificate is one page: its serial text and its free texts share this many characters. */
  pageBudgetCharacters?: number;
}

export const DEFAULT_LIMITS: SerialLimits = { maxCharactersPerCertificate: 600, maxLengthPerSerialNumber: 60, maxSerialNumbers: 5000 };

export interface ParsedSerials {
  serials: string[];
  /** Entries dropped because the same serial (any letter case) came before. */
  duplicates: number;
  /** Entries longer than the limit; they stay in the list so the person can fix them. */
  tooLong: number;
}

/** `PREFIX0001` .. `PREFIX{n}`, four digits at least. */
export function generateSerials(prefix: string, quantity: number, limits = DEFAULT_LIMITS): string[] {
  const n = Math.max(0, Math.min(limits.maxSerialNumbers, Math.floor(quantity) || 0));
  return Array.from({ length: n }, (_, i) => prefix + String(i + 1).padStart(4, '0'));
}

/**
 * Reads pasted text: one serial per line (commas and semicolons also separate), blank lines dropped, repeats (case-insensitive,
 * as the backend checks them) counted and kept once. A line like `CS-26-0001 to CS-26-0100` (or `..`) expands to the range.
 */
export function parseSerials(text: string, limits = DEFAULT_LIMITS): ParsedSerials {
  const seen = new Set<string>();
  const serials: string[] = [];
  let duplicates = 0;
  const add = (s: string) => {
    const key = s.toLowerCase();
    if (seen.has(key)) duplicates++;
    else {
      seen.add(key);
      serials.push(s);
    }
  };
  for (const raw of text.split(/[\n,;]+/)) {
    const line = raw.trim();
    if (!line) continue;
    const range = /^(.*?)(\d+)\s*(?:to|\.\.|–|—)\s*\1(\d+)$/i.exec(line);
    if (range && range[2].length === range[3].length) {
      const [, prefix, a, b] = range;
      const from = Number(a);
      const to = Number(b);
      if (to >= from && to - from < limits.maxSerialNumbers) {
        for (let i = from; i <= to; i++) add(prefix + String(i).padStart(a.length, '0'));
        continue;
      }
    }
    add(line);
  }
  const capped = serials.slice(0, limits.maxSerialNumbers);
  return { serials: capped, duplicates, tooLong: capped.filter((s) => s.length > limits.maxLengthPerSerialNumber).length };
}

/** Characters one certificate needs for these serials: they are joined with ", ". */
export const joinedLength = (serials: readonly string[]): number =>
  serials.reduce((a, s) => a + s.length, 0) + Math.max(0, serials.length - 1) * 2;

/** Splits the serials over `count` certificates as evenly as possible, in order (the backend does the same); never an empty one. */
export function splitSerials(serials: readonly string[], count: number): string[][] {
  if (!serials.length) return [[]];
  const parts = Math.max(1, Math.min(Math.floor(count) || 1, serials.length));
  const base = Math.floor(serials.length / parts);
  const extra = serials.length % parts;
  const out: string[][] = [];
  let at = 0;
  for (let i = 0; i < parts; i++) {
    const size = base + (i < extra ? 1 : 0);
    out.push(serials.slice(at, at + size));
    at += size;
  }
  return out;
}

/** Share of the printed serial box the fullest certificate uses; above 1 the serials would not fit. */
export function fillRatio(parts: readonly (readonly string[])[], limits = DEFAULT_LIMITS): number {
  return Math.max(0, ...parts.map(joinedLength)) / limits.maxCharactersPerCertificate;
}

/** The fewest certificates the serials fit on (what the backend makes when no count is given). */
export function suggestedCertificateCount(serials: readonly string[], limits = DEFAULT_LIMITS): number {
  if (serials.length <= 1) return 1;
  let lo = 1;
  let hi = serials.length;
  while (lo < hi) {
    const mid = (lo + hi) >> 1;
    if (fillRatio(splitSerials(serials, mid), limits) <= 1) hi = mid;
    else lo = mid + 1;
  }
  return lo;
}
