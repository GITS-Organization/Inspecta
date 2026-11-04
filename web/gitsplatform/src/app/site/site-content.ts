import { environment } from '../../environments/environment';

/**
 * ALL the words, the mark and the contact details of the public site live here.
 * GITS has no final logo or brand copy yet: everything below is placeholder, replace it in this one file.
 */
export const brand = {
  name: 'GITS',
  /** Placeholder mark: a ring with a gap on an ink tile (drawn in CSS, see `.mark` in styles.scss). Swap for a real logo later. */
  markLabel: 'GITS home',
};

export const nav = [
  { label: 'What we do', fragment: 'what' },
  { label: 'Clients', fragment: 'clients' },
  { label: 'About us', fragment: 'about' },
  { label: 'Contact', fragment: 'contact' },
];

export const hero = {
  title: 'Inspection paperwork that proves itself.',
  lead: 'GITS builds the platform inspection companies run on: quotations, site inspections, reviewed certificates, and a QR code that lets anyone check a certificate is real.',
  primary: { label: 'Talk to us', fragment: 'contact' },
  secondary: { label: 'See how it works', fragment: 'what' },
  demoHint: 'Press the code to see what a client sees when they scan a certificate.',
  codeLabel: 'Or check a real certificate code',
};

/** The static demo shown when the demo code is pressed. */
export const demoCertificate = {
  issuedBy: 'Integrity',
  client: 'Al Noor Industries',
  item: 'Air compressor',
  number: 'CERT-2026-0412',
  expires: '14 May 2027',
};

export const what = {
  title: 'One job, from request to certificate.',
  lead: 'Every inspection follows the same path, and each step has an owner and a record.',
  steps: [
    { title: 'Request', text: "The client's email becomes a request automatically." },
    { title: 'Quotation', text: "The coordinator sends a priced offer by email, from the company's own branding." },
    { title: 'Inspection', text: 'The inspector fills the certificate for every machine on site.' },
    { title: 'Review', text: 'The technical manager checks the whole job and approves it.' },
    { title: 'Handover', text: 'Numbered PDFs with QR codes are filed under the client and emailed.' },
  ],
  pillars: [
    { title: 'Branded per company', text: 'Each inspection company gets its own logo, colors, accreditations and email identity on every document.' },
    { title: 'Checked by a person', text: 'Nothing reaches a client until a technical manager has reviewed the certificates as they will print.' },
    { title: 'Verifiable by anyone', text: 'Scanning the QR opens a public page that shows what was issued, to whom, and when it expires.' },
  ],
};

export const clients = {
  title: 'Running at Integrity.',
  lead: 'Our first company on the platform issues its certificates, quotations and client archive here.',
  list: [
    {
      name: 'Integrity',
      tagline: 'For inspection and engineering solutions',
      logo: 'assets/integrity-logo.png',
      url: environment.integrityUrl,
      cta: 'Open integrity.gitsplatform.com',
    },
  ],
};

export const about = {
  title: 'Built by people who read inspection forms.',
  paragraphs: [
    'GITS started from a simple problem: inspection companies lose days moving the same information between emails, Word files and spreadsheets.',
    'We turned that into one workflow with the real forms, the real review step, and one place to find any certificate years later.',
  ],
};

export const contact = {
  title: 'Want your company on it?',
  email: 'hello@gitsplatform.com',
  phone: '+20 000 000 0000',
  note: '© 2026 GITS. Placeholder copy and contact details, to be replaced.',
};
