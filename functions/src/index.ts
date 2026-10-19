import * as admin from "firebase-admin";
import { defineSecret, defineString } from "firebase-functions/params";
import { HttpsError, onCall } from "firebase-functions/v2/https";
import { onMessagePublished } from "firebase-functions/v2/pubsub";
import { onSchedule } from "firebase-functions/v2/scheduler";
import { syncNewMessages } from "./sync";
import { renewWatch } from "./watch";

admin.initializeApp();

const gmailKey = defineSecret("GMAIL_DELEGATED_SA_KEY");
// Plain params, not secrets — neither value is sensitive on its own.
// Declaring them here is enough for `firebase deploy` to prompt for
// them and persist the answers to functions/.env; every function then
// reads them straight off `process.env` (see gmail.ts) without needing
// to be listed in that function's own options the way secrets must be.
defineString("GMAIL_MAILBOX");
const gmailTopic = defineString("GMAIL_PUBSUB_TOPIC");

/**
 * Fires when Gmail pushes a new-mail notification for the intake
 * mailbox to `GMAIL_PUBSUB_TOPIC` (registered by `renewGmailWatch`).
 * The notification only says "something changed" — the actual new
 * message(s) are found via `syncNewMessages`'s history diff.
 */
export const gmailPushNotification = onMessagePublished(
  { topic: gmailTopic.value(), secrets: [gmailKey] },
  async () => {
    const result = await syncNewMessages();
    console.log("gmailPushNotification", result);
  },
);

/**
 * Gmail's watch() expires after 7 days — renew comfortably inside that
 * window. Also performs the very first watch() call the pipeline needs;
 * see functions/README.md for the one-time setup this depends on
 * (domain-wide delegation authorized in the Workspace Admin console,
 * and the Pub/Sub topic's publish grant for Gmail).
 */
export const renewGmailWatch = onSchedule(
  { schedule: "every 24 hours", secrets: [gmailKey] },
  async () => {
    const result = await renewWatch();
    console.log("renewGmailWatch", result);
  },
);

async function requireCoordinatorOrAdmin(uid: string | undefined): Promise<void> {
  if (!uid) throw new HttpsError("unauthenticated", "Sign in first.");
  const profile = await admin.firestore().doc(`users/${uid}`).get();
  const data = profile.data();
  const role = data?.role;
  const active = data?.active === true;
  // "supervisor" was folded into "coordinator"; accounts still stored with
  // the old value keep their access.
  const isCoordinator = role === "coordinator" || role === "supervisor";
  if (!active || (!isCoordinator && role !== "admin")) {
    throw new HttpsError("permission-denied", "Only an active coordinator or admin can do this.");
  }
}

/**
 * Manual on-demand sync, for testing the pipeline without waiting on a
 * real push notification (which needs the full Workspace/Pub-Sub setup
 * working end to end first) — call this from the app or `firebase
 * functions:shell` / the Firebase console after deploying to verify new
 * mail in the intake inbox shows up as a request.
 */
export const syncGmailNow = onCall({ secrets: [gmailKey] }, async (request) => {
  await requireCoordinatorOrAdmin(request.auth?.uid);
  return syncNewMessages();
});
