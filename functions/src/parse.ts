import { gmail_v1 } from "googleapis";

export interface ParsedEmail {
  fromEmail: string;
  fromName: string | null;
  subject: string | null;
  receivedAt: Date;
  bodyText: string;
}

export interface ParsedItem {
  type: string;
  capacity: string | null;
  quantity: number;
}

function decodeBase64Url(data: string): string {
  return Buffer.from(data, "base64url").toString("utf-8");
}

function stripHtml(html: string): string {
  return html
    .replace(/<style[\s\S]*?<\/style>/gi, " ")
    .replace(/<script[\s\S]*?<\/script>/gi, " ")
    .replace(/<br\s*\/?>/gi, "\n")
    .replace(/<\/p>/gi, "\n")
    .replace(/<[^>]+>/g, " ")
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/[ \t]+/g, " ")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

/** Walks a message's MIME tree, preferring `text/plain` over `text/html`. */
function extractBody(payload: gmail_v1.Schema$MessagePart | undefined): string {
  if (!payload) return "";

  let plain: string | null = null;
  let html: string | null = null;

  function walk(part: gmail_v1.Schema$MessagePart) {
    const data = part.body?.data;
    if (data) {
      if (part.mimeType === "text/plain" && !plain) plain = decodeBase64Url(data);
      if (part.mimeType === "text/html" && !html) html = decodeBase64Url(data);
    }
    for (const child of part.parts ?? []) walk(child);
  }
  walk(payload);

  if (plain) return (plain as string).trim();
  if (html) return stripHtml(html);
  return "";
}

function headerValue(headers: gmail_v1.Schema$MessagePartHeader[] | undefined, name: string): string | null {
  return headers?.find((h) => h.name?.toLowerCase() === name.toLowerCase())?.value ?? null;
}

/** Parses `"Name" <email@example.com>` (or just a bare address) into parts. */
function parseFromHeader(raw: string | null): { email: string; name: string | null } {
  if (!raw) return { email: "", name: null };
  const match = raw.match(/^\s*"?([^"<]*?)"?\s*<([^>]+)>\s*$/);
  if (match) {
    const name = match[1].trim();
    return { email: match[2].trim().toLowerCase(), name: name.length > 0 ? name : null };
  }
  return { email: raw.trim().toLowerCase(), name: null };
}

export function parseMessage(message: gmail_v1.Schema$Message): ParsedEmail {
  const headers = message.payload?.headers;
  const from = parseFromHeader(headerValue(headers, "From"));
  const dateHeader = headerValue(headers, "Date");
  const receivedAt = dateHeader ? new Date(dateHeader) : new Date(Number(message.internalDate ?? Date.now()));

  return {
    fromEmail: from.email,
    fromName: from.name,
    subject: headerValue(headers, "Subject"),
    receivedAt: isNaN(receivedAt.getTime()) ? new Date() : receivedAt,
    bodyText: extractBody(message.payload),
  };
}

/**
 * Light heuristics only (deliberately not a real parser — see
 * functions/README.md): scans the body for known equipment keywords and a
 * nearby quantity/capacity. Misses anything phrased differently, and can
 * misfire on unrelated text that happens to contain a keyword. A
 * coordinator always reviews and can fix the result in-app (the "Complete
 * request" sheet), so this only needs to save typing on the common case,
 * not be exhaustive.
 */
const EQUIPMENT_KEYWORDS = [
  "overhead crane",
  "mobile crane",
  "gantry crane",
  "tower crane",
  "crane",
  "forklift",
  "scissor lift",
  "boom lift",
  "lift",
  "hoist",
  "pressure vessel",
  "boiler",
  "compressor",
  "elevator",
  "generator",
  "excavator",
  "conveyor",
];

const QUANTITY_RE = /(\d+)\s*(?:x|units?)\b/i;
const CAPACITY_RE = /(\d+(?:\.\d+)?\s?(?:t|ton|tons|tonnes|kg|bar|kn)\b(?:[^,.\n]{0,20})?)/i;

export function extractItems(bodyText: string): ParsedItem[] {
  const lines = bodyText.split(/\r?\n|(?<=[.!?])\s+/);
  const seen = new Set<string>();
  const items: ParsedItem[] = [];

  for (const line of lines) {
    const lower = line.toLowerCase();
    const keyword = EQUIPMENT_KEYWORDS.find((k) => lower.includes(k));
    if (!keyword || seen.has(keyword)) continue;
    seen.add(keyword);

    const quantityMatch = line.match(QUANTITY_RE);
    const capacityMatch = line.match(CAPACITY_RE);

    items.push({
      type: keyword.replace(/\b\w/g, (c) => c.toUpperCase()),
      capacity: capacityMatch ? capacityMatch[1].trim() : null,
      quantity: quantityMatch ? Math.max(1, parseInt(quantityMatch[1], 10)) : 1,
    });
  }

  return items;
}

const LOCATION_LABEL_RE = /(?:location|site|address)\s*[:\-]\s*(.+)/i;
const LOCATION_KEYWORD_RE =
  /([A-Z][\w'-]*(?:\s+[A-Z0-9][\w'-]*){0,4}\s+(?:Port|Zone|Plant|Factory|Warehouse|Facility|Site))/;

export function extractLocation(bodyText: string): string | null {
  const labelMatch = bodyText.match(LOCATION_LABEL_RE);
  if (labelMatch) return labelMatch[1].trim().split(/\r?\n/)[0].trim();

  const keywordMatch = bodyText.match(LOCATION_KEYWORD_RE);
  return keywordMatch ? keywordMatch[1].trim() : null;
}
