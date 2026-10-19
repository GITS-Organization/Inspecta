# Product

## Register

brand

## Users

The public website (`website/`) is read by decision-makers at organisations that carry out or commission thorough examinations of industrial equipment: operations and technical managers, inspection-body supervisors and coordinators, QA/compliance leads. They are comparing tools, usually on a laptop at their desk, sometimes forwarded a link on a phone. Many work in both English and Arabic, so both must be first-class (Arabic with true RTL layout).

The job to be done on the site: understand in seconds what Inspecta is, see the real app (not a stock dashboard), judge whether it fits their workflow, and ask for a demo.

## Product Purpose

Inspecta is a mobile-first (also runs in the browser) inspection-management app built on Flutter and Firebase. It carries one job from a client's request to a signed certificate: request intake, quotation negotiation, inspector assignment and scheduling, a guided digital "Report of Thorough Examination", technical-manager review and signature, and timesheets. Four working roles: supervisor, coordinator, inspector, technical manager (admin exists, user management is not built yet). English and Arabic.

Success for the website: a visitor can accurately describe what Inspecta does, has explored the real screens through the interactive explorer, and submits the demo request. It must never claim anything the app does not do. Known non-features (do not imply): photo/attachment capture in the report, push notifications, analytics dashboards, a corrective-action tracker, a client portal, emailing quotes or certificates from inside the app.

## Brand Personality

Exact, accountable, calm. The voice of a stamped inspection tag: plain statements, no hype, no invented numbers. Confident because it is specific.

Existing brand to preserve: blue `#2563EB` (white-and-blue app palette), rounded check-mark-in-a-circle mark, Inter (Latin) and Cairo (Arabic) typefaces, slate neutrals.

## Anti-references

- Generic blue-gradient SaaS landing templates, hero-metric rows with big invented numbers, identical icon-card grids.
- Fake logos, testimonials, certifications, savings percentages, "trusted by" strips.
- Stock dashboards that do not match the real app; the explorer must use the app's actual screens, labels and workflow.
- TÜV Austria Egypt branding, logos or accreditation badges (they appear in the repo's certificate template but are not to be shown publicly).
- Glassmorphism, gradient text, side-stripe card borders, eyebrow-above-every-heading scaffolding.

## Design Principles

1. **Show the real thing.** The product explorer renders the app's real screens, statuses and labels with clearly labelled sample data; the site is the demo.
2. **Say only what is true.** Every claim maps to shipped behaviour; gaps are phrased neutrally or left out. Contact details are never invented.
3. **The job is the story.** Organise the page around the lifecycle Request, Quote, Assign, Inspect, Certify, Sent, not around a feature checklist.
4. **Inspection-tag clarity.** Verdicts (safe, safe with conditions, not safe) are the one place colour speaks loudly; everything else stays quiet blue and slate.
5. **Bilingual by construction.** Logical properties, RTL-safe components, real Arabic copy and Arabic app UI in the explorer, not a bolted-on translation.

## Accessibility & Inclusion

Target WCAG 2.2 AA: 4.5:1 body contrast, visible focus states, full keyboard operation of nav, explorer, FAQ and form, semantic landmarks and heading order, `prefers-reduced-motion` respected, form errors announced. Never rely on colour alone for status (chips carry text). RTL and Arabic typography tested.
