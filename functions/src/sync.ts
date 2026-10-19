import * as admin from "firebase-admin";
import { gmail_v1 } from "googleapis";
import { gmailClient } from "./gmail";
import { extractItems, extractLocation, parseMessage } from "./parse";

const SYNC_DOC = "system/gmailSync";
const REQUESTS_COLLECTION = "requests";
const PREVIEW_LENGTH = 240;

function db() {
  return admin.firestore();
}

/** Bootstraps the sync cursor on first run without processing the mailbox's
 * entire pre-existing history as "new" requests. */
async function currentHistoryId(gmail: gmail_v1.Gmail): Promise<string> {
  const profile = await gmail.users.getProfile({ userId: "me" });
  const historyId = profile.data.historyId;
  if (!historyId) throw new Error("Gmail profile has no historyId.");
  return historyId;
}

async function findRequestByThreadId(threadId: string): Promise<FirebaseFirestore.QueryDocumentSnapshot | null> {
  const snapshot = await db()
    .collection(REQUESTS_COLLECTION)
    .where("emailThreadId", "==", threadId)
    .limit(1)
    .get();
  return snapshot.empty ? null : snapshot.docs[0];
}

/**
 * Incrementally syncs new inbound mail since the last processed
 * historyId. Safe to call repeatedly/concurrently-ish: each message is
 * looked up by its Gmail id (used as the `requests` doc id) or, for a
 * reply on an already-known thread, folded into that request instead of
 * creating a duplicate — so re-processing the same history range is a
 * no-op the second time.
 *
 * Returns how many new requests were created and how many existing ones
 * were updated with a client reply, for the manual-trigger endpoint to
 * report back.
 */
export async function syncNewMessages(): Promise<{ created: number; repliesLogged: number }> {
  const gmail = gmailClient();
  const syncDoc = await db().doc(SYNC_DOC).get();
  const lastHistoryId = syncDoc.data()?.lastHistoryId as string | undefined;

  if (!lastHistoryId) {
    // First run: remember where we are, but don't treat the whole mailbox
    // as new. `renewGmailWatch`'s initial call normally sets this first;
    // this is a fallback if `syncGmailNow` runs before that ever has.
    const historyId = await currentHistoryId(gmail);
    await db().doc(SYNC_DOC).set({ lastHistoryId: historyId }, { merge: true });
    return { created: 0, repliesLogged: 0 };
  }

  const messageIds = new Set<string>();
  let newestHistoryId = lastHistoryId;
  let pageToken: string | undefined;

  do {
    const response = await gmail.users.history.list({
      userId: "me",
      startHistoryId: lastHistoryId,
      historyTypes: ["messageAdded"],
      pageToken,
    });
    for (const record of response.data.history ?? []) {
      for (const added of record.messagesAdded ?? []) {
        const id = added.message?.id;
        if (id) messageIds.add(id);
      }
    }
    if (response.data.historyId) newestHistoryId = response.data.historyId;
    pageToken = response.data.nextPageToken ?? undefined;
  } while (pageToken);

  let created = 0;
  let repliesLogged = 0;

  for (const messageId of messageIds) {
    const outcome = await processMessage(gmail, messageId);
    if (outcome === "created") created++;
    if (outcome === "reply") repliesLogged++;
  }

  await db().doc(SYNC_DOC).set({ lastHistoryId: newestHistoryId }, { merge: true });
  return { created, repliesLogged };
}

async function processMessage(
  gmail: gmail_v1.Gmail,
  messageId: string,
): Promise<"created" | "reply" | "skipped"> {
  // Idempotent: the request doc is keyed by the Gmail message id, so a
  // history range processed twice (Pub/Sub is at-least-once) is a no-op.
  const existing = await db().collection(REQUESTS_COLLECTION).doc(messageId).get();
  if (existing.exists) return "skipped";

  const full = await gmail.users.messages.get({ userId: "me", id: messageId, format: "full" });
  const labelIds = full.data.labelIds ?? [];
  // A dedicated intake mailbox's INBOX is inbound-only in the normal
  // case, but SENT/DRAFT guard against replies-from-this-mailbox or a
  // misconfigured label set from still being ingested as client requests.
  if (!labelIds.includes("INBOX") || labelIds.includes("SENT") || labelIds.includes("DRAFT")) {
    return "skipped";
  }

  const threadId = full.data.threadId;
  const parsed = parseMessage(full.data);
  if (!parsed.fromEmail) return "skipped";

  const preview = parsed.bodyText.slice(0, PREVIEW_LENGTH).trim();

  if (threadId) {
    const existingForThread = await findRequestByThreadId(threadId);
    if (existingForThread) {
      // A reply on an already-known request's thread — BR-03.6's "client
      // replied" flag, not a second request for the same job.
      await existingForThread.ref.update({
        hasUnreadClientReply: true,
        lastReplySnippet: preview,
      });
      return "reply";
    }
  }

  const items = extractItems(parsed.bodyText);
  const location = extractLocation(parsed.bodyText);

  await db()
    .collection(REQUESTS_COLLECTION)
    .doc(messageId)
    .set({
      source: "email",
      clientId: null,
      // Best-effort display name until a coordinator matches the sender
      // to a client (see MatchClientSheet) — mirrors an unmatched
      // sender's `isNewClient` treatment elsewhere in the app.
      clientName: parsed.fromName ?? parsed.fromEmail,
      emailThreadId: threadId ?? null,
      subject: parsed.subject,
      emailFrom: parsed.fromEmail,
      emailPreview: preview,
      receivedAt: admin.firestore.Timestamp.fromDate(parsed.receivedAt),
      location: location ?? "",
      preferredDate: null,
      accessNotes: null,
      status: "request_received",
      coordinatorId: null,
      items: items.map((item, index) => ({
        id: `item-${messageId}-${index}`,
        type: item.type,
        category: null,
        manufacturer: null,
        model: null,
        serialNumber: null,
        capacity: item.capacity,
        quantity: item.quantity,
      })),
      hasUnreadClientReply: false,
      lastReplySnippet: null,
      rejectReason: null,
    });

  return "created";
}
