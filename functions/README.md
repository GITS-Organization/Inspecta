# Gmail → requests intake pipeline

Watches a Google Workspace mailbox (e.g. `requests@yourcompany.com`) and
turns each new inbound email into a `requests/{id}` Firestore document —
the same collection the Flutter app's Requests inbox reads from
(`RequestsRemoteDataSource.watchRequests()`). A reply on an
already-known thread updates that request's `hasUnreadClientReply` /
`lastReplySnippet` instead of creating a duplicate.

```
Gmail inbox ──watch()──▶ Pub/Sub topic ──push──▶ gmailPushNotification
                                                        │
                                                        ▼
                                            syncNewMessages() (src/sync.ts)
                                              • diffs Gmail history
                                              • parses each new message
                                              • writes requests/{gmailMessageId}
                                                        │
                                                        ▼
                                              Flutter app (Firestore listener)
```

`renewGmailWatch` runs on a daily schedule to keep the `watch()`
subscription alive (Gmail expires it after 7 days) and, on its very
first run, seeds the sync cursor. `syncGmailNow` is a callable HTTPS
function for testing on demand without waiting on a real email + the
full push pipeline.

**Parsing is light heuristics only** (keyword + regex matching in
`src/parse.ts`), not a real NLP/AI parser — it'll miss anything phrased
unusually and can occasionally misfire. It's meant to save typing on the
common case, not be exhaustive. Equipment items and location extracted
this way (or left blank when nothing matched) can always be fixed
afterwards in the app via the request detail screen's **"Complete
request"** sheet — that UI was added alongside this pipeline for exactly
this reason. Sender-to-client matching is *not* attempted here either —
every email-sourced request is created with `clientId: null`, exactly
like the (already-built) "unmatched sender" case elsewhere in the app;
a coordinator matches it from the Clients tab as usual.

## One-time setup

These steps involve your Google Workspace admin console and Google
Cloud project — nothing here can be done from this repo or by an
assistant without your account access, so they're written out in full.

### 1. Enable the Gmail API

In the GCP project (`inspecta-68798`): **APIs & Services → Enable APIs →
Gmail API → Enable**.

### 2. Create the delegated service account

1. **IAM & Admin → Service Accounts → Create service account**, e.g.
   `gmail-ingest@inspecta-68798.iam.gserviceaccount.com`. No project
   roles are needed on it — it only ever acts as the impersonated Gmail
   user, never as itself against GCP resources.
2. Open it → **Keys → Add key → Create new key → JSON**. Download it;
   you'll paste its contents into a Firebase secret in step 5 (delete
   the local file afterwards — don't commit it).
3. Copy the service account's **Client ID** (a long numeric string,
   shown on its Details tab) — you need it for the next step.

### 3. Authorize it for domain-wide delegation (Workspace Admin console)

This is the step that actually lets the service account read the
mailbox — skipping it means every Gmail API call will fail with a
permission error, however correct everything else is.

1. Go to **admin.google.com → Security → Access and data control → API
   controls → Domain-wide delegation → Add new**.
2. **Client ID**: paste the service account's Client ID from step 2.3.
3. **OAuth scopes**: `https://www.googleapis.com/auth/gmail.readonly`
4. Authorize. You need Workspace super-admin rights to do this.

### 4. Create the Pub/Sub topic and grant Gmail publish rights

```bash
gcloud config set project inspecta-68798
gcloud pubsub topics create gmail-requests-inbox

# Required by Gmail's watch() API for every project that uses it —
# gmail-api-push@system.gserviceaccount.com is Google's own fixed
# publisher identity, not something specific to this project.
gcloud pubsub topics add-iam-policy-binding gmail-requests-inbox \
  --member="serviceAccount:gmail-api-push@system.gserviceaccount.com" \
  --role="roles/pubsub.publisher"
```

### 5. Set the Firebase config this code reads

From `functions/`:

```bash
firebase functions:secrets:set GMAIL_DELEGATED_SA_KEY
# paste the full JSON key file contents from step 2.2, then Ctrl-D

firebase deploy --only functions
# the CLI will interactively prompt for GMAIL_MAILBOX and
# GMAIL_PUBSUB_TOPIC (the two plain, non-secret params `defineString`
# declares in src/index.ts) the first time, and save your answers to
# functions/.env — answer with the intake mailbox address (e.g.
# requests@yourcompany.com) and gmail-requests-inbox respectively.
```

### 6. Bootstrap the watch subscription

`renewGmailWatch` runs daily on its own, but the pipeline has no
subscription at all until it's called once. Trigger it manually after
deploying — easiest from the [Firebase console](https://console.firebase.google.com/)
(Functions → `renewGmailWatch` → the "⋮" menu → **Test function**, or
just wait up to 24h for its first scheduled run), or:

```bash
gcloud scheduler jobs run firebase-schedule-renewGmailWatch-us-central1 \
  --location=us-central1
```

(adjust the region suffix if you deployed elsewhere — `firebase
functions:log` after deploying shows the exact job name.)

## Testing

- **Fastest**: call `syncGmailNow` (an `onCall` function) from the app
  or `firebase functions:shell` while signed in as a coordinator/admin —
  it runs the same sync logic on demand and returns
  `{ created, repliesLogged }`. Send yourself a test email to the
  intake mailbox first, then call it.
- **End to end**: send a real email to the intake mailbox from an
  external address and wait — Gmail's push notification should reach
  `gmailPushNotification` within seconds, and the new `requests/{id}`
  doc should show up in the app's Requests inbox (New tab) immediately
  (it's a live Firestore listener). Check `firebase functions:log` if
  it doesn't.
- A reply sent on the *same thread* (e.g. hit Reply on the test email
  and send again) should update the original request
  (`hasUnreadClientReply: true`) rather than create a second one.

## Operational notes

- **Cost**: Gmail API, Pub/Sub and Cloud Functions all have generous
  free tiers that comfortably cover a single low-volume intake mailbox.
  The only thing likely to cost anything at this scale is Cloud
  Scheduler's job (`renewGmailWatch`'s trigger), which is a few cents/mo
  at most.
- **Multiple mailboxes**: not supported by this version — `GMAIL_MAILBOX`
  is a single address. Extending to several would mean looping over a
  list of mailboxes in `renewWatch`/`syncNewMessages` and namespacing
  the sync cursor (`system/gmailSync`) per mailbox.
- **Rules**: no `firestore.rules` changes were needed — these functions
  write via the Admin SDK, which bypasses Security Rules entirely.
