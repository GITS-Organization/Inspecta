import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;

import 'proposal_pdf_style.dart';

/// The proposal's fixed pages — the same for every client.

// ---- Table of contents -----------------------------------------------------

const _contents = <(String, String)>[
  ('1', 'Cover Letter'),
  ('2', 'About Us'),
  ('3', 'Our Vision'),
  ('4', 'Integrity Services'),
  ('5', 'Scope of Work'),
  ('6', 'Project Budget'),
  ('7', 'Terms and Conditions'),
];

List<pw.Widget> proposalContentsPage() => [
  proposalSectionTitle('Table of Contents'),
  pw.SizedBox(height: 20),
  for (final (number, title) in _contents)
    pw.Container(
      margin: const pw.EdgeInsets.only(bottom: 10),
      padding: const pw.EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: pw.BoxDecoration(
        color: PdfColors.white,
        border: pw.Border.all(color: ProposalColors.border, width: 0.8),
        borderRadius: const pw.BorderRadius.all(pw.Radius.circular(8)),
      ),
      child: pw.Row(
        children: [
          pw.Container(
            width: 26,
            height: 26,
            alignment: pw.Alignment.center,
            decoration: const pw.BoxDecoration(color: ProposalColors.navy, shape: pw.BoxShape.circle),
            child: pw.Text(
              number,
              style: pw.TextStyle(fontSize: 11, fontWeight: pw.FontWeight.bold, color: PdfColors.white),
            ),
          ),
          pw.SizedBox(width: 14),
          pw.Text(
            title,
            style: pw.TextStyle(fontSize: 13, fontWeight: pw.FontWeight.bold, color: ProposalColors.navy),
          ),
        ],
      ),
    ),
];

// ---- Cover letter -----------------------------------------------------------

List<pw.Widget> proposalCoverLetterPage({
  required String? attention,
  required String clientName,
  required String subject,
  required String proposalNumber,
  required String preparedByName,
  required String preparedByTitle,
}) {
  pw.Widget paragraph(String text) => pw.Padding(
    padding: const pw.EdgeInsets.only(bottom: 12),
    child: proposalRich(text, fontSize: 10.5),
  );

  return [
    proposalSectionTitle('1-Cover Letter:'),
    pw.SizedBox(height: 20),
    paragraph('Dear ${attention ?? 'Sir / Madam'},'),
    paragraph(
      'Thank you for your enquiry. **Integrity International for Inspection** is pleased to submit '
      'this technical and commercial offer to **$clientName** for **$subject** '
      '(reference $proposalNumber).',
    ),
    paragraph(
      'This document introduces our company and services, sets out the scope of work and '
      'deliverables, the price of the work, and the terms and conditions that apply to it.',
    ),
    paragraph(
      'We trust the offer meets your requirements and we look forward to working with you. '
      'Please do not hesitate to contact us if you need any clarification.',
    ),
    pw.SizedBox(height: 14),
    paragraph('Yours sincerely,'),
    pw.SizedBox(height: 16),
    pw.Text(
      preparedByName,
      style: pw.TextStyle(fontSize: 12, fontWeight: pw.FontWeight.bold, color: ProposalColors.navy),
    ),
    pw.Text(preparedByTitle, style: const pw.TextStyle(fontSize: 10, color: ProposalColors.muted)),
    pw.Text(proposalIssuer, style: const pw.TextStyle(fontSize: 10, color: ProposalColors.muted)),
  ];
}

// ---- About us / vision -----------------------------------------------------

List<pw.Widget> proposalAboutPage() => [
  proposalSectionTitle('2-About Us:'),
  pw.SizedBox(height: 12),
  proposalRich(
    '**INTEGRITY** (Environmental & Industrial Services) is a leading provider of testing, '
    'inspection, certification, maintenance, and consulting services. With years of experience '
    'and a team of dedicated experts, we specialize in ensuring compliance with international '
    'standards, enhancing workplace safety, and driving sustainable business practices. Our '
    'multidisciplinary approach caters to industries such as manufacturing, energy, construction, '
    'and more, delivering tailored solutions to meet unique client needs.',
    fontSize: 10.5,
  ),
  pw.SizedBox(height: 22),
  proposalSectionTitle('3-Our Vision:'),
  pw.SizedBox(height: 12),
  proposalRich(
    'To be the most trusted partner for industries worldwide, driving innovation and '
    'sustainability while ensuring safety, compliance, and operational excellence. We aim to '
    'create a positive impact on the environment and society through our expertise and '
    'commitment to quality.',
    fontSize: 10.5,
  ),
];

// ---- Services ---------------------------------------------------------------

const _industrialServices = <(String, String)>[
  ('Third-Party Quality Services', 'QA/QC during fabrication, painting, erection, assembly, and dismantling.'),
  ('Calibration Services', 'Precise calibration for tanks, silos, and industrial equipment.'),
  ('Failure & Corrosion Analysis', 'Comprehensive failure investigation and coupon analysis.'),
  ('Supply Chain Services', 'Supplier evaluation, third-party inspections, progress monitoring, and pre-shipment checks.'),
  ('Lifting Equipment Inspections & Training', 'Inspection and certification for lifting gears, cranes, forklifts, scaffolding, and more.'),
  ('Non-Destructive Testing (NDT)', 'Ultrasonic, magnetic particle, dye penetrant, radiographic, and thermal imaging.'),
  ('Advanced NDT Testing', 'Phased array, long-range ultrasonic testing (LRUT), pulsed eddy current (PEC), etc.'),
  ('Chemical & Mechanical Testing', 'Material testing for steel and polymer products.'),
  ('Factory Audits & Inspections', 'Detailed assessments for quality assurance and compliance.'),
  ('Energy Efficiency Audits', 'Optimizing energy performance to reduce costs and emissions.'),
  ('Mechanical, Electrical, and Civil Works Supervision', 'Comprehensive oversight to ensure project safety and quality.'),
  ('Mechanical and Asset Integrity', 'Safeguarding operations through systematic integrity assessments.'),
  ('Product Certification', 'CE marking and international certifications for various products.'),
  ('Material Testing', 'Testing of material properties, composition, and quality.'),
  ('Health & Safety (Process Safety Study)', 'Comprehensive evaluation of industrial processes to identify hazards, assess risks, and implement process safety management measures.'),
  ('Mechanical and Electrical Maintenance', 'Preventive and corrective maintenance of mechanical and electrical equipment.'),
];

List<pw.Widget> proposalIndustrialServicesPage() => [
  proposalSectionTitle('4-Integrity Services:'),
  proposalSubTitle('4-1 Industrial Services'),
  pw.Table(
    border: const pw.TableBorder(
      top: pw.BorderSide(color: ProposalColors.navy, width: 1),
      bottom: pw.BorderSide(color: ProposalColors.border, width: 0.8),
      horizontalInside: pw.BorderSide(color: ProposalColors.border, width: 0.6),
    ),
    columnWidths: const {0: pw.FlexColumnWidth(1), 1: pw.FlexColumnWidth(1.25)},
    children: [
      pw.TableRow(
        repeat: true,
        decoration: const pw.BoxDecoration(color: ProposalColors.navy),
        children: [proposalCell('Service', header: true), proposalCell('Scope', header: true)],
      ),
      for (var i = 0; i < _industrialServices.length; i++)
        pw.TableRow(
          decoration: pw.BoxDecoration(color: i.isOdd ? ProposalColors.rowAlt : PdfColors.white),
          children: [
            proposalCell('${i + 1}-  ${_industrialServices[i].$1}', bold: true, color: ProposalColors.navy, vertical: 5),
            proposalCell(_industrialServices[i].$2, vertical: 5),
          ],
        ),
    ],
  ),
];

const _environmentalServices = <(String, List<(String, String)>)>[
  ('Environmental Services', [
    ('Air Quality Monitoring', 'Ambient air sampling, stack sampling, dust and gas analysis.'),
    ('Water & Wastewater Sampling & Testing', 'Chemical, microbiological, and heavy metals testing.'),
    ('Soil & Sediment Sampling', 'Contamination assessment, heavy metals, hydrocarbons.'),
    ('Biodiversity & Ecological Surveys', 'Flora, fauna, and ecosystem studies.'),
    ('Noise & Vibration Monitoring', 'Workplace and environmental monitoring.'),
    ('Environmental Auditing', 'Compliance audits, impact assessments, and reporting.'),
  ]),
  ('Industrial Hygiene', [
    ('Occupational Health Assessment', 'Exposure monitoring, ergonomics, workplace hazards.'),
    ('Chemical & Biological Hazard Assessment', 'Identification and measurement of workplace hazards.'),
    ('Asbestos Surveys', 'Inspection, sampling, and risk assessment.'),
    ('Health & Safety Studies', 'Process safety, risk assessment, mitigation planning.'),
    ('Industrial Hygiene Auditing', 'Compliance checks and best practices.'),
  ]),
  ('Laboratory Services', [
    ('Chemical Analysis', 'Testing for metals, organics, pollutants.'),
    ('Microbiological Analysis', 'Water, soil, and air samples for bacteria, fungi.'),
    ('Material Testing', 'Corrosion, composition, quality control.'),
    ('Environmental Sample Analysis', 'Lab testing for air, water, soil, and waste.'),
  ]),
  ('Climate Change & Sustainability Services', [
    ('Carbon Footprint Assessment', 'GHG emissions measurement.'),
    ('Carbon Accounting & Reporting', 'ISO 14064, GHG Protocol, CDP Reporting.'),
    ('Climate Risk & Resilience Assessment', 'Operational and supply chain risks.'),
    ('Net Zero Strategy & Roadmap', 'Emissions reduction planning.'),
    ('Carbon Offset & Credit Solutions', 'Offset project management.'),
    ('Sustainability Strategy Development', 'ESG strategy and policies.'),
    ('Sustainability Reporting & Disclosure', 'GRI, SASB, TCFD reports.'),
    ('Circular Economy & Resource Efficiency', 'Waste reduction and recycling.'),
    ('Renewable Energy & Energy Efficiency', 'Renewable energy projects.'),
    ('Sustainable Procurement & Supply Chain', 'Supplier ESG evaluation.'),
  ]),
];

List<pw.Widget> proposalEnvironmentalServicesPage() => [
  proposalSectionTitle('4-Integrity Services:'),
  proposalSubTitle('4-2 Environmental Services'),
  pw.Table(
    border: const pw.TableBorder(
      top: pw.BorderSide(color: ProposalColors.navy, width: 1),
      bottom: pw.BorderSide(color: ProposalColors.border, width: 0.8),
      horizontalInside: pw.BorderSide(color: ProposalColors.border, width: 0.6),
    ),
    columnWidths: const {0: pw.FlexColumnWidth(1), 1: pw.FlexColumnWidth(1.25)},
    children: [
      pw.TableRow(
        repeat: true,
        decoration: const pw.BoxDecoration(color: ProposalColors.navy),
        children: [proposalCell('Service / Activity', header: true), proposalCell('Description / Scope', header: true)],
      ),
      for (final (category, services) in _environmentalServices) ...[
        pw.TableRow(
          decoration: const pw.BoxDecoration(color: ProposalColors.panel),
          children: [
            proposalCell(category.toUpperCase(), bold: true, color: ProposalColors.greenDark, vertical: 5, fontSize: 8.5),
            pw.SizedBox(),
          ],
        ),
        for (var i = 0; i < services.length; i++)
          pw.TableRow(
            decoration: pw.BoxDecoration(color: i.isOdd ? ProposalColors.rowAlt : PdfColors.white),
            children: [
              proposalCell(services[i].$1, bold: true, color: ProposalColors.navy, vertical: 4, fontSize: 8.5),
              proposalCell(services[i].$2, vertical: 4, fontSize: 8.5),
            ],
          ),
      ],
    ],
  ),
];

// ---- Terms and conditions ---------------------------------------------------

pw.Widget _termsHeading(String text) => pw.Padding(
  padding: const pw.EdgeInsets.only(top: 10, bottom: 5),
  child: pw.Text(
    text,
    style: pw.TextStyle(fontSize: 11, fontWeight: pw.FontWeight.bold, color: ProposalColors.navy),
  ),
);

const _bankAccounts = <List<String>>[
  ['Integrity International for Inspection', 'Commercial International Bank (CIB)', 'EGP', '100072853193', 'EG640010008900000100072853193', 'CIBEEGCX089'],
  ['Integrity International for Inspection', 'Commercial International Bank (CIB)', 'USD', '100072840806', 'EG570010008900000100072840806', 'CIBEEGCX089'],
];

List<pw.Widget> proposalTermsPage() => [
  proposalSectionTitle('7-Terms and conditions:'),
  _termsHeading('7.1 Invoicing and Payment'),
  proposalPoint('INTEGRITY will issue and submit an invoice upon completion of the job.'),
  proposalPoint('The Client agrees to pay 100 % against the reports.'),
  _termsHeading('7.2 Offer Validity'),
  proposalPoint('This proposal is made in good faith and remains valid for **14 days** from the date of issue.'),
  _termsHeading('7.3 Payment details'),
  proposalPoint('Payments can be made via cash or check.'),
  proposalPoint('Bank details will be provided upon request.'),
  _termsHeading('7.4 Bank Account'),
  pw.Table(
    border: const pw.TableBorder(
      top: pw.BorderSide(color: ProposalColors.navy, width: 1),
      bottom: pw.BorderSide(color: ProposalColors.border, width: 0.8),
      horizontalInside: pw.BorderSide(color: ProposalColors.border, width: 0.6),
    ),
    columnWidths: const {
      0: pw.FlexColumnWidth(1.5),
      1: pw.FlexColumnWidth(1.5),
      2: pw.FlexColumnWidth(1.05),
      3: pw.FlexColumnWidth(1.45),
      4: pw.FlexColumnWidth(2.6),
      5: pw.FlexColumnWidth(1.2),
    },
    children: [
      pw.TableRow(
        decoration: const pw.BoxDecoration(color: ProposalColors.navy),
        children: [
          for (final head in const ['Beneficiary Name', 'Bank Name', 'Currency', 'Account Number', 'IBAN', 'SWIFT Code'])
            proposalCell(head, header: true, fontSize: 7.5, vertical: 5),
        ],
      ),
      for (var i = 0; i < _bankAccounts.length; i++)
        pw.TableRow(
          decoration: pw.BoxDecoration(color: i.isOdd ? ProposalColors.rowAlt : PdfColors.white),
          children: [for (final value in _bankAccounts[i]) proposalCell(value, fontSize: 7.5, vertical: 5)],
        ),
    ],
  ),
  _termsHeading('7.5 INTEGRITY Obligations'),
  proposalPoint('Upon request, INTEGRITY shall mobilize the spare parts to the client site.'),
  proposalPoint(
    'All tasks will be executed in compliance with INTEGRITY **QHSE rules and regulations**, as '
    'well as safety practices and procedures.',
  ),
  _termsHeading('7.6 Client Obligations'),
  proposalPoint(
    'The Client is responsible for obtaining all required **site/gate passes, permits, safety '
    'cards, and inductions** prior to the mobilization of INTEGRITY’s team.',
  ),
  proposalPoint(
    'The safety of equipment and the inspection personnel during the inspection will be the '
    'Client’s responsibility, including any associated costs and arrangements.',
  ),
  proposalPoint(
    'The Client shall provide full support to INTEGRITY’s team for proper execution of the job. '
    'This includes the provision of related instruments, safety equipment, and emergency medical '
    'facilities.',
  ),
  proposalPoint(
    'The Client’s authorized representative or contractor at the site shall coordinate with '
    'INTEGRITY team to ensure all necessary arrangements are in place to facilitate the work.',
  ),
  proposalPoint(
    'INTEGRITY shall not be held liable for delays caused by the Client, its subcontractors, or '
    'unforeseen circumstances beyond INTEGRITY control.',
  ),
  pw.SizedBox(height: 18),
  pw.Center(
    child: pw.Text(
      '==== End of Quotation ====',
      style: pw.TextStyle(fontSize: 11, fontWeight: pw.FontWeight.bold, color: ProposalColors.navy),
    ),
  ),
];

// ---- Thank you --------------------------------------------------------------

pw.Widget proposalThankYouPage(pw.ImageProvider logo) {
  const page = PdfPageFormat.a4;
  return pw.Stack(
    children: [
      pw.Positioned(
        left: 0,
        right: 0,
        bottom: 0,
        child: proposalStripe(height: 14),
      ),
      pw.Positioned(
        left: 0,
        right: 0,
        top: page.height * 0.28,
        child: pw.Column(
          children: [
            pw.Image(logo, height: 90, fit: pw.BoxFit.contain),
            pw.SizedBox(height: 40),
            pw.Text(
              'THANK YOU',
              style: pw.TextStyle(
                fontSize: 44,
                fontWeight: pw.FontWeight.bold,
                color: ProposalColors.navy,
                letterSpacing: 4,
              ),
            ),
            pw.SizedBox(height: 14),
            pw.Row(
              mainAxisAlignment: pw.MainAxisAlignment.center,
              children: [
                pw.Container(width: 46, height: 4, color: ProposalColors.green),
                pw.SizedBox(width: 10),
                pw.Container(width: 46, height: 4, color: ProposalColors.navy),
              ],
            ),
            pw.SizedBox(height: 26),
            pw.Text(
              'We appreciate the opportunity to work with you.',
              style: const pw.TextStyle(fontSize: 12, color: ProposalColors.muted),
            ),
            pw.SizedBox(height: 40),
            pw.Text(
              proposalIssuer,
              style: pw.TextStyle(fontSize: 11, fontWeight: pw.FontWeight.bold, color: ProposalColors.navy),
            ),
            pw.SizedBox(height: 4),
            for (final line in const [
              '21 Nakib Sharif Ramzy st, Smouha, Alexandria, Egypt',
              '+20 100 091 4066 | +20 155 325 6481',
              'info@integrity-intl.com  |  www.integrity-intl.com',
            ])
              pw.Padding(
                padding: const pw.EdgeInsets.only(top: 2),
                child: pw.Text(line, style: const pw.TextStyle(fontSize: 9.5, color: ProposalColors.muted)),
              ),
          ],
        ),
      ),
    ],
  );
}
