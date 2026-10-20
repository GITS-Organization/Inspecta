import 'dart:typed_data';

import 'package:flutter/services.dart' show rootBundle;
import 'package:intl/intl.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;

import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/request_item.dart';
import '../../domain/entities/quote_line_item.dart';
import 'proposal_pdf_content.dart';
import 'proposal_pdf_style.dart';

/// What the proposal says about this particular quote, beyond its lines.
class ProposalDetails {
  final String proposalNumber;
  final String quotationNumber;
  final DateTime date;
  final DateTime validUntil;

  /// The client contact the offer is addressed to, when there is one.
  final String? attention;
  final String preparedByName;
  final String preparedByTitle;

  const ProposalDetails({
    required this.proposalNumber,
    required this.quotationNumber,
    required this.date,
    required this.validUntil,
    required this.preparedByName,
    required this.preparedByTitle,
    this.attention,
  });
}

/// Renders Integrity International for Inspection's "Technical and
/// Commercial Offer" — what a price offer is sent to the client as, instead
/// of text.
///
/// Ten pages: the cover, contents, cover letter, about us and vision, the
/// services (two pages), the scope of work and the project budget — the
/// pages that depend on the quote — the terms and conditions, and a thank-you
/// page. Like the certificate it is a document for the client, so it stays
/// English whatever language the app is in.
Future<Uint8List> buildProposalPdf({
  required InspectionRequest request,
  required List<QuoteLineItem> lines,
  required List<String> notes,
  required ProposalDetails details,
}) async {
  final regular = pw.Font.ttf(await rootBundle.load('assets/fonts/inter/Inter-Regular.ttf'));
  final bold = pw.Font.ttf(await rootBundle.load('assets/fonts/inter/Inter-Bold.ttf'));
  final arabic = pw.Font.ttf(await rootBundle.load('assets/fonts/cairo/Cairo-Regular.ttf'));
  final arabicBold = pw.Font.ttf(await rootBundle.load('assets/fonts/cairo/Cairo-Bold.ttf'));
  final logo = pw.MemoryImage((await rootBundle.load('assets/pngs/integraty-logo.png')).buffer.asUint8List());
  final cover = pw.MemoryImage((await rootBundle.load('assets/pngs/proposal_cover.png')).buffer.asUint8List());

  final doc = pw.Document(
    title: 'Technical and Commercial Offer ${details.proposalNumber}',
    author: proposalIssuer,
    creator: proposalIssuer,
  );
  final theme = pw.ThemeData.withFont(base: regular, bold: bold, fontFallback: [arabic, arabicBold]);

  pw.PageTheme pageTheme() => pw.PageTheme(
    pageFormat: PdfPageFormat.a4,
    margin: const pw.EdgeInsets.fromLTRB(40, 28, 40, 24),
    theme: theme,
    buildBackground: proposalGridBackground,
  );

  final subject = _subject(request);
  final header = ProposalHeader(logo: logo, subject: subject, number: details.proposalNumber, date: details.date);

  void contentPage(List<pw.Widget> Function() build) {
    doc.addPage(
      pw.MultiPage(
        pageTheme: pageTheme(),
        header: header.build,
        footer: proposalFooter,
        build: (_) => build(),
      ),
    );
  }

  doc.addPage(
    pw.Page(
      pageTheme: pw.PageTheme(pageFormat: PdfPageFormat.a4, margin: pw.EdgeInsets.zero, theme: theme),
      build: (_) => _coverPage(cover, request, subject, details),
    ),
  );
  contentPage(proposalContentsPage);
  contentPage(
    () => proposalCoverLetterPage(
      attention: details.attention,
      clientName: request.clientName,
      subject: subject,
      proposalNumber: details.proposalNumber,
      preparedByName: details.preparedByName,
      preparedByTitle: details.preparedByTitle,
    ),
  );
  contentPage(proposalAboutPage);
  contentPage(proposalIndustrialServicesPage);
  contentPage(proposalEnvironmentalServicesPage);
  contentPage(() => _scopePage(request));
  contentPage(() => _budgetPage(request, lines, notes));
  contentPage(proposalTermsPage);
  doc.addPage(
    pw.Page(
      pageTheme: pw.PageTheme(
        pageFormat: PdfPageFormat.a4,
        margin: pw.EdgeInsets.zero,
        theme: theme,
        buildBackground: proposalGridBackground,
      ),
      build: (_) => proposalThankYouPage(logo),
    ),
  );

  return doc.save();
}

/// "NDT Testing"-style subject: the distinct item types.
String _subject(InspectionRequest request) {
  final types = _distinct(request.items.map((item) => item.type));
  return types.isEmpty ? 'Inspection services' : types.join(', ');
}

List<String> _distinct(Iterable<String> values) {
  final seen = <String>{};
  return [
    for (final value in values.map((v) => v.trim()))
      if (value.isNotEmpty && seen.add(value.toLowerCase())) value,
  ];
}

// ---- Cover ------------------------------------------------------------------

/// The cover is the designed artwork; only what differs per quote is printed
/// over it, on white patches that hide the sample values it was drawn with.
/// Positions are in the artwork's own pixels (1588 × 2246).
pw.Widget _coverPage(
  pw.ImageProvider cover,
  InspectionRequest request,
  String subject,
  ProposalDetails details,
) {
  const artWidth = 1588.0;
  final scale = PdfPageFormat.a4.width / artWidth;

  pw.Widget field(
    String text, {
    required double x,
    required double y,
    required double width,
    required double height,
    required double size,
    PdfColor color = ProposalColors.navySoft,
    pw.FontWeight weight = pw.FontWeight.bold,
  }) {
    return pw.Positioned(
      left: x * scale,
      top: y * scale,
      child: pw.Container(
        width: width * scale,
        height: height * scale,
        color: PdfColors.white,
        alignment: pw.Alignment.centerLeft,
        child: pw.FittedBox(
          fit: pw.BoxFit.scaleDown,
          alignment: pw.Alignment.centerLeft,
          child: pw.Text(text, style: pw.TextStyle(fontSize: size, fontWeight: weight, color: color)),
        ),
      ),
    );
  }

  return pw.Stack(
    children: [
      pw.Positioned.fill(child: pw.Image(cover, fit: pw.BoxFit.fill)),
      field(request.clientName, x: 452, y: 1216, width: 800, height: 108, size: 32),
      field(details.attention ?? '—', x: 372, y: 1426, width: 410, height: 78, size: 23),
      field(_subject(request), x: 927, y: 1426, width: 400, height: 78, size: 23),
      field(details.quotationNumber, x: 295, y: 1601, width: 395, height: 62, size: 23),
      field(proposalDate(details.date), x: 740, y: 1601, width: 246, height: 62, size: 23),
      field(
        proposalDate(details.validUntil),
        x: 1040,
        y: 1601,
        width: 290,
        height: 62,
        size: 23,
        color: ProposalColors.greenDark,
      ),
      field(details.preparedByName, x: 215, y: 1937, width: 520, height: 66, size: 24),
      field(
        details.preparedByTitle,
        x: 215,
        y: 2005,
        width: 520,
        height: 38,
        size: 11,
        color: ProposalColors.muted,
        weight: pw.FontWeight.normal,
      ),
    ],
  );
}

// ---- Page: 5-Scope of work ---------------------------------------------------

List<pw.Widget> _scopePage(InspectionRequest request) {
  final scope = _distinct(request.items.map((item) => item.type));

  return [
    proposalSectionTitle('5-Scope of work:'),
    pw.SizedBox(height: 10),
    for (var i = 0; i < scope.length; i++)
      pw.Padding(
        padding: const pw.EdgeInsets.only(left: 14, bottom: 5),
        child: pw.Row(
          crossAxisAlignment: pw.CrossAxisAlignment.start,
          children: [
            pw.Container(
              width: 18,
              height: 18,
              margin: const pw.EdgeInsets.only(right: 10),
              alignment: pw.Alignment.center,
              decoration: const pw.BoxDecoration(color: ProposalColors.navy, shape: pw.BoxShape.circle),
              child: pw.Text(
                '${i + 1}',
                style: pw.TextStyle(fontSize: 8.5, fontWeight: pw.FontWeight.bold, color: PdfColors.white),
              ),
            ),
            pw.Padding(
              padding: const pw.EdgeInsets.only(top: 3),
              child: pw.Text(scope[i], style: const pw.TextStyle(fontSize: 10.5, color: ProposalColors.ink)),
            ),
          ],
        ),
      ),
    pw.SizedBox(height: 14),
    pw.Table(
      border: const pw.TableBorder(
        top: pw.BorderSide(color: ProposalColors.navy, width: 1),
        bottom: pw.BorderSide(color: ProposalColors.border, width: 0.8),
        horizontalInside: pw.BorderSide(color: ProposalColors.border, width: 0.6),
      ),
      columnWidths: const {
        0: pw.FlexColumnWidth(2.2),
        1: pw.FlexColumnWidth(4),
        2: pw.FlexColumnWidth(1.1),
        3: pw.FlexColumnWidth(2),
      },
      children: [
        pw.TableRow(
          repeat: true,
          decoration: const pw.BoxDecoration(color: ProposalColors.navy),
          children: [
            proposalCell('PR number', header: true, align: pw.Alignment.center, vertical: 8),
            proposalCell('Description', header: true, vertical: 8),
            proposalCell('Qty', header: true, align: pw.Alignment.center, vertical: 8),
            proposalCell('Unit of Measure', header: true, align: pw.Alignment.center, vertical: 8),
          ],
        ),
        for (var i = 0; i < request.items.length; i++)
          pw.TableRow(
            decoration: pw.BoxDecoration(color: i.isOdd ? ProposalColors.rowAlt : PdfColors.white),
            children: [
              proposalCell(
                request.items[i].prNumber?.trim().isNotEmpty == true ? request.items[i].prNumber!.trim() : '—',
                align: pw.Alignment.center,
                vertical: 10,
              ),
              proposalCell(_describe(request.items[i]), vertical: 10),
              proposalCell('${request.items[i].quantity}', align: pw.Alignment.center, vertical: 10),
              proposalCell('AU', align: pw.Alignment.center, vertical: 10),
            ],
          ),
      ],
    ),
  ];
}

/// "Overhead crane — Konecranes CXT, 10 t": what the row is, then any make,
/// model and capacity on file.
String _describe(RequestItem item) {
  String? clean(String? v) => v == null || v.trim().isEmpty ? null : v.trim();
  final make = [clean(item.manufacturer), clean(item.model)].whereType<String>().join(' ');
  final detail = [make, clean(item.capacity)].where((v) => v != null && v.isNotEmpty).join(', ');
  return detail.isEmpty ? item.type : '${item.type} — $detail';
}

// ---- Page: 6-Project Budget --------------------------------------------------

String _egp(int piastres) => '${NumberFormat('#,##0.##', 'en').format(piastres / 100)} EGP';

List<pw.Widget> _budgetPage(InspectionRequest request, List<QuoteLineItem> lines, List<String> notes) {
  final grandTotal = lines.fold<int>(0, (sum, line) => sum + line.lineTotalPiastres);

  pw.Widget head(String text) => proposalCell(text, header: true, align: pw.Alignment.center, vertical: 8, fontSize: 8.5);

  return [
    proposalSectionTitle('6-Project Budget:'),
    pw.SizedBox(height: 14),
    pw.Table(
      border: const pw.TableBorder(
        top: pw.BorderSide(color: ProposalColors.navy, width: 1),
        horizontalInside: pw.BorderSide(color: ProposalColors.border, width: 0.6),
      ),
      columnWidths: const {
        0: pw.FlexColumnWidth(0.8),
        1: pw.FlexColumnWidth(4),
        2: pw.FlexColumnWidth(1.6),
        3: pw.FlexColumnWidth(1.9),
        4: pw.FlexColumnWidth(0.9),
        5: pw.FlexColumnWidth(1.9),
      },
      children: [
        pw.TableRow(
          repeat: true,
          decoration: const pw.BoxDecoration(color: ProposalColors.navy),
          children: [
            head('Item'),
            head('Description'),
            head('Unit of Pricing'),
            head('Cost Per Item (EGP)'),
            head('Qty'),
            head('Total Cost (EGP)'),
          ],
        ),
        for (var i = 0; i < lines.length; i++)
          pw.TableRow(
            decoration: pw.BoxDecoration(color: i.isOdd ? ProposalColors.rowAlt : PdfColors.white),
            children: [
              proposalCell('${i + 1}', align: pw.Alignment.center, vertical: 11),
              _lineDescription(request, lines[i]),
              proposalCell(lines[i].pricingUnit.pdfLabel, align: pw.Alignment.center, vertical: 11),
              proposalCell(_egp(lines[i].unitPricePiastres), align: pw.Alignment.center, vertical: 11),
              proposalCell('${lines[i].quantity}', align: pw.Alignment.center, vertical: 11),
              proposalCell(_egp(lines[i].lineTotalPiastres), align: pw.Alignment.center, vertical: 11),
            ],
          ),
        if (lines.length > 1)
          pw.TableRow(
            decoration: const pw.BoxDecoration(color: ProposalColors.panel),
            children: [
              pw.SizedBox(),
              pw.SizedBox(),
              pw.SizedBox(),
              pw.SizedBox(),
              proposalCell('Total', bold: true, color: ProposalColors.navy, align: pw.Alignment.center),
              proposalCell(_egp(grandTotal), bold: true, color: ProposalColors.navy, align: pw.Alignment.center),
            ],
          ),
      ],
    ),
    pw.Container(
      width: double.infinity,
      padding: const pw.EdgeInsets.symmetric(vertical: 6),
      alignment: pw.Alignment.center,
      decoration: const pw.BoxDecoration(
        color: ProposalColors.greenDark,
        borderRadius: pw.BorderRadius.only(
          bottomLeft: pw.Radius.circular(4),
          bottomRight: pw.Radius.circular(4),
        ),
      ),
      child: pw.Text(
        '14% VAT will be added',
        style: pw.TextStyle(fontSize: 9.5, fontWeight: pw.FontWeight.bold, color: PdfColors.white),
      ),
    ),
    pw.SizedBox(height: 20),
    ...proposalNotesPanel(notes),
  ];
}

/// The item's description bullet — the type, with whatever capacity is on
/// the request item it was priced from.
pw.Widget _lineDescription(InspectionRequest request, QuoteLineItem line) {
  final item = request.items.where((i) => i.id == line.requestItemId).firstOrNull;
  final text = item == null ? line.type : _describe(item);
  return pw.Container(
    padding: const pw.EdgeInsets.symmetric(horizontal: 8, vertical: 11),
    child: pw.Row(
      crossAxisAlignment: pw.CrossAxisAlignment.start,
      children: [
        pw.Padding(
          padding: const pw.EdgeInsets.only(top: 3.5, right: 6),
          child: pw.Container(
            width: 3.5,
            height: 3.5,
            decoration: const pw.BoxDecoration(color: ProposalColors.green, shape: pw.BoxShape.circle),
          ),
        ),
        pw.Expanded(child: pw.Text(text, style: const pw.TextStyle(fontSize: 9, color: ProposalColors.ink))),
      ],
    ),
  );
}
