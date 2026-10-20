# Inspecta website

The public marketing site for Inspecta: a single-page product presentation with an interactive "walk through the real app" explorer, in English and Arabic (true RTL). It is **separate from the Flutter app** (`lib/`, `web/`): plain HTML, CSS and JavaScript, no build step, no dependencies, no external requests (fonts are the app's own Inter and Cairo, subset to woff2).

## Run it

```bash
node website/serve.mjs        # http://localhost:8780
```

Any static host works (it is just files). `?lang=ar` / `?lang=en` forces a language; otherwise the saved choice, then the browser language, decide.

## Layout

| Path | What |
| --- | --- |
| `index.html` | All sections. English copy lives in the HTML (crawlable); each translated element carries its Arabic in a `data-ar` attribute (`data-ar-placeholder`, `data-ar-aria-label`, `data-ar-content` for attributes). |
| `privacy.html` | Short, factual privacy notice for what the form actually does. Needs the legal entity, retention period and a contact address added (see below). |
| `assets/css/site.css` | Tokens (OKLCH, brand blue `#2563EB`), layout, sections. Logical properties throughout. |
| `assets/css/app-ui.css` | HTML/CSS replica of the app's UI (phone frame + components). Colours mirror `lib/core/consts/app_colors.dart`. Sized in `em`, so a screen scales with the device. |
| `assets/js/screens.js` | The ten explorer screens and page fragments, English and Arabic, with their interactions (tabs, yes/no, wizard steps, assign picker, signature pad). Labels mirror `lib/l10n/app_en.arb` / `app_ar.arb`. |
| `assets/js/site.js` | Language switch, header/menu, hero toasts, explorer, "Try it" trail, form validation and submit. |
| `assets/js/config.js` | Where the contact form delivers (see below). |
| `serve.mjs` | Dependency-free static server for local preview. |

Page flow: hero (badge, headline, the six-step staircase, three fact stats, phones with cycling in-app toasts), problems, solutions, explorer, features, **Try it**, how it works, industries, benefits, comparison, about, **Where we are**, FAQ, contact, footer with a closing band. The nav is Solutions, Features, How It Works, Try It, Where We Are, FAQ plus the demo button; Industries and About stay reachable from the footer. The EN / العربية pill in the header sets the language (`data-set-lang`).

**Try it** (`#try`, rendered by `renderTrail` in `site.js`) shows one examination result in the four places the app puts it: the inspector's Conclusion option, the review banner, the certificate PDF's conclusion line (`certificate_pdf_builder.dart`: Satisfactory / Satisfactory with conditions / Not satisfactory) and the sent-list badge. The wording is the app's (`reviewResult*`, `reviewBadge*`, `*Option` in the `.arb` files). If the app changes how a result is worded or coloured, change `RESULTS` in `site.js`.

**Where we are** (`#where`) is two lists written from the code: what the app does today, and what it does not (photo attachments, push notifications, analytics, client portal, emailing from the app, Arabic certificate PDF). It is deliberately not a roadmap: no future plans are stated. Keep both lists in step with the app, and with the FAQ.

The hero stats (6 steps, 3 roles, 2 languages) are product facts, not metrics. The hero toasts cycle strings that exist in the app (`taskAcceptedMessage`, `certificateSubmittedMessage`, and the review approve and return messages).

To add or change an explorer screen: add the template in `screens.js`, a button in the `.xp-list` and a `.xp-panel` article (with the same id) in `index.html`.

## Contact form (needs one deploy step)

"Request a Demo" and "Contact Our Team" post a document to the `demoRequests` collection of the Firebase project through Firestore's REST endpoint. No API key or credential is in the page. `firestore.rules` has a new `demoRequests` block: anyone may **create** a well-formed document (every field is type- and size-bounded), only an `admin` can read, nobody can update or delete from a client.

**Until the rules are deployed the form shows its error state** (HTTP 403). Deploy with:

```bash
firebase deploy --only firestore:rules
```

Read submissions in the Firebase console (Firestore, `demoRequests`). There is no rate limiting beyond a client-side 30 s guard and a honeypot field, so consider App Check or a Cloud Function in front of it before heavy traffic.

## What the copy claims (and does not)

Every statement was checked against the app's code. Deliberately **not** claimed: photo/attachment capture in the report, push notifications, analytics dashboards, a corrective-action tracker, a client portal, or emailing quotes/certificates from inside the app (approving a certificate records it as sent to the client's certificates email; the repository documents that the email itself is not sent by the app). The certificate PDF is English-only today and the FAQ says so.

Worth confirming before launch:

- **Mailbox intake** ("Requests from a connected mailbox") is described from `functions/src/` (Gmail to `requests`). Confirm it is deployed and set up for the audience.
- **Industries** are inferred from the app's equipment categories (Lifts, Cranes, Pressure) and sample data; the "construction" and "inspection bodies" entries are positioning, not stated customers.
- **Platforms** ("phone-first, also runs in a browser") rather than named stores; add store links once published.
- **Arabic copy** was written alongside the app's own Arabic strings; have a native reader review it.
- **Privacy notice**: add the legal entity, retention period and a direct contact address. No terms page is included because none exists.
- No customer logos, testimonials, certifications or statistics appear, by design. TÜV Austria Egypt material found in the repo (certificate template, logos) is intentionally not used.

## Before it goes public

It is live at **https://inspecta-68798.web.app** (Firebase Hosting, `hosting` block in `firebase.json`, public dir `website/`). Redeploy after any change with:

```bash
firebase deploy --only hosting
```

`canonical`, `og:image` and a `sitemap.xml` still need the final domain; connect a custom domain under Hosting in the Firebase console when you have one.
