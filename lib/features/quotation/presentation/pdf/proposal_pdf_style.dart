import 'package:intl/intl.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;

/// The proposal's look, taken from its cover: deep navy and signal green on a
/// faint engineering grid, with a hazard-stripe accent bar.
abstract final class ProposalColors {
  static const navy = PdfColor.fromInt(0xFF13285D);
  static const navySoft = PdfColor.fromInt(0xFF1A346B);
  static const green = PdfColor.fromInt(0xFF3FA14D);
  static const greenDark = PdfColor.fromInt(0xFF237936);
  static const gridLine = PdfColor.fromInt(0xFFE6EDF7);
  static const panel = PdfColor.fromInt(0xFFEEF3FA);
  static const rowAlt = PdfColor.fromInt(0xFFF4F7FC);
  static const border = PdfColor.fromInt(0xFFCFDAEA);
  static const ink = PdfColor.fromInt(0xFF1B2433);
  static const muted = PdfColor.fromInt(0xFF5B6B85);
}

const proposalIssuer = 'Integrity International for Inspection';

String proposalDate(DateTime date) => DateFormat('dd-MM-yyyy').format(date);

/// A faint grid behind every page, as on the cover.
pw.Widget proposalGridBackground(pw.Context context) {
  const size = PdfPageFormat.a4;
  return pw.FullPage(
    ignoreMargins: true,
    child: pw.CustomPaint(
      size: PdfPoint(size.width, size.height),
      painter: (canvas, box) {
        canvas
          ..setStrokeColor(ProposalColors.gridLine)
          ..setLineWidth(0.5);
        const step = 26.0;
        for (var x = 0.0; x <= box.x; x += step) {
          canvas
            ..moveTo(x, 0)
            ..lineTo(x, box.y)
            ..strokePath();
        }
        for (var y = 0.0; y <= box.y; y += step) {
          canvas
            ..moveTo(0, y)
            ..lineTo(box.x, y)
            ..strokePath();
        }
      },
    ),
  );
}

/// The navy-and-green hazard stripe from the cover's "FOR" board.
pw.Widget proposalStripe({double height = 5, double? width}) {
  return pw.CustomPaint(
    size: PdfPoint(width ?? PdfPageFormat.a4.width, height),
    painter: (canvas, box) {
      canvas
        ..saveContext()
        ..drawRect(0, 0, box.x, box.y)
        ..clipPath()
        ..setFillColor(ProposalColors.navy)
        ..drawRect(0, 0, box.x, box.y)
        ..fillPath()
        ..setFillColor(ProposalColors.green);
      final stripe = box.y * 1.4;
      for (var x = -box.y; x < box.x + box.y; x += stripe * 2) {
        canvas
          ..moveTo(x, 0)
          ..lineTo(x + stripe, 0)
          ..lineTo(x + stripe + box.y, box.y)
          ..lineTo(x + box.y, box.y)
          ..closePath()
          ..fillPath();
      }
      canvas.restoreContext();
    },
  );
}

/// What every content page starts with: logo, title and subject, and the
/// date / proposal number / page, over the stripe bar.
class ProposalHeader {
  final pw.ImageProvider logo;
  final String subject;
  final String number;
  final DateTime date;

  const ProposalHeader({
    required this.logo,
    required this.subject,
    required this.number,
    required this.date,
  });

  pw.Widget build(pw.Context context) {
    pw.Widget meta(String label, String value) => pw.Padding(
      padding: const pw.EdgeInsets.only(bottom: 3),
      child: pw.Row(
        crossAxisAlignment: pw.CrossAxisAlignment.start,
        children: [
          pw.SizedBox(
            width: 64,
            child: pw.Text(
              label,
              style: pw.TextStyle(fontSize: 7.5, fontWeight: pw.FontWeight.bold, color: ProposalColors.muted),
            ),
          ),
          pw.Expanded(
            child: pw.Text(
              value,
              style: pw.TextStyle(fontSize: 8, fontWeight: pw.FontWeight.bold, color: ProposalColors.navy),
            ),
          ),
        ],
      ),
    );

    return pw.Column(
      crossAxisAlignment: pw.CrossAxisAlignment.stretch,
      children: [
        pw.Row(
          crossAxisAlignment: pw.CrossAxisAlignment.center,
          children: [
            pw.SizedBox(
              width: 118,
              child: pw.Image(logo, height: 50, fit: pw.BoxFit.contain, alignment: pw.Alignment.centerLeft),
            ),
            pw.Expanded(
              child: pw.Column(
                children: [
                  pw.Text(
                    'TECHNICAL AND COMMERCIAL OFFER',
                    textAlign: pw.TextAlign.center,
                    style: pw.TextStyle(
                      fontSize: 11.5,
                      fontWeight: pw.FontWeight.bold,
                      color: ProposalColors.navy,
                      letterSpacing: 0.4,
                    ),
                  ),
                  pw.SizedBox(height: 4),
                  pw.Text(
                    subject,
                    textAlign: pw.TextAlign.center,
                    maxLines: 2,
                    style: const pw.TextStyle(fontSize: 9, color: ProposalColors.greenDark),
                  ),
                ],
              ),
            ),
            pw.SizedBox(
              width: 148,
              child: pw.Column(
                crossAxisAlignment: pw.CrossAxisAlignment.start,
                children: [
                  meta('Date', proposalDate(date)),
                  meta('Proposal No.', number),
                  meta('Page', '${context.pageNumber}'),
                ],
              ),
            ),
          ],
        ),
        pw.SizedBox(height: 7),
        proposalStripe(height: 4.5, width: PdfPageFormat.a4.width - 80),
        pw.SizedBox(height: 14),
      ],
    );
  }
}

pw.Widget proposalFooter(pw.Context context) {
  const small = pw.TextStyle(fontSize: 7.5, color: ProposalColors.muted);
  return pw.Column(
    crossAxisAlignment: pw.CrossAxisAlignment.stretch,
    children: [
      pw.Container(height: 0.8, color: ProposalColors.navy),
      pw.SizedBox(height: 5),
      pw.Row(
        crossAxisAlignment: pw.CrossAxisAlignment.start,
        children: [
          pw.Expanded(
            child: pw.Column(
              crossAxisAlignment: pw.CrossAxisAlignment.start,
              children: [
                pw.Text(
                  proposalIssuer,
                  style: pw.TextStyle(fontSize: 8, fontWeight: pw.FontWeight.bold, color: ProposalColors.navy),
                ),
                pw.Text('21 Nakib Sharif Ramzy st, Smouha, Alexandria, Egypt', style: small),
                pw.Text('+20 100 091 4066 | +20 155 325 6481', style: small),
              ],
            ),
          ),
          pw.Expanded(
            child: pw.Column(
              crossAxisAlignment: pw.CrossAxisAlignment.end,
              children: [
                pw.Text('info@integrity-intl.com', style: small),
                pw.Text('www.integrity-intl.com', style: small),
                pw.Text('This document is confidential and intended solely for the recipient.', style: small),
              ],
            ),
          ),
        ],
      ),
    ],
  );
}

/// "5-Scope of work:" — a pale panel with a green marker.
pw.Widget proposalSectionTitle(String text) => pw.Container(
  width: double.infinity,
  decoration: const pw.BoxDecoration(
    color: ProposalColors.panel,
    borderRadius: pw.BorderRadius.all(pw.Radius.circular(4)),
  ),
  child: pw.Row(
    children: [
      pw.Container(
        width: 6,
        height: 28,
        decoration: const pw.BoxDecoration(
          color: ProposalColors.green,
          borderRadius: pw.BorderRadius.only(topLeft: pw.Radius.circular(4), bottomLeft: pw.Radius.circular(4)),
        ),
      ),
      pw.SizedBox(width: 10),
      pw.Text(
        text,
        style: pw.TextStyle(fontSize: 14.5, fontWeight: pw.FontWeight.bold, color: ProposalColors.navy),
      ),
    ],
  ),
);

pw.Widget proposalSubTitle(String text) => pw.Padding(
  padding: const pw.EdgeInsets.only(top: 12, bottom: 6),
  child: pw.Text(
    text,
    style: pw.TextStyle(fontSize: 11, fontWeight: pw.FontWeight.bold, color: ProposalColors.greenDark),
  ),
);

/// A table cell: navy header, white/zebra body.
pw.Widget proposalCell(
  String text, {
  bool header = false,
  bool bold = false,
  PdfColor? color,
  pw.Alignment align = pw.Alignment.centerLeft,
  double vertical = 6,
  double fontSize = 9,
}) => pw.Container(
  alignment: align,
  padding: pw.EdgeInsets.symmetric(horizontal: 8, vertical: vertical),
  child: pw.Text(
    text,
    textAlign: align == pw.Alignment.center ? pw.TextAlign.center : pw.TextAlign.left,
    style: pw.TextStyle(
      fontSize: fontSize,
      fontWeight: header || bold ? pw.FontWeight.bold : pw.FontWeight.normal,
      color: color ?? (header ? PdfColors.white : ProposalColors.ink),
    ),
  ),
);

/// A small green arrow marking a point in a list.
pw.Widget proposalArrow() => pw.Padding(
  padding: const pw.EdgeInsets.only(top: 2.5, right: 7),
  child: pw.CustomPaint(
    size: const PdfPoint(6, 7),
    painter: (canvas, size) {
      canvas
        ..setFillColor(ProposalColors.green)
        ..moveTo(0, 0)
        ..lineTo(size.x, size.y / 2)
        ..lineTo(0, size.y)
        ..fillPath();
    },
  ),
);

/// One point of a list: [proposalArrow] and wrapped text. A `**bold**` run
/// in [text] is printed bold.
pw.Widget proposalPoint(String text, {double fontSize = 9.5, double left = 12}) => pw.Padding(
  padding: pw.EdgeInsets.only(left: left, bottom: 4),
  child: pw.Row(
    crossAxisAlignment: pw.CrossAxisAlignment.start,
    children: [
      proposalArrow(),
      pw.Expanded(child: proposalRich(text, fontSize: fontSize)),
    ],
  ),
);

/// Text where `**…**` marks bold runs.
pw.Widget proposalRich(String text, {double fontSize = 9.5, PdfColor? color}) {
  final spans = <pw.InlineSpan>[];
  final parts = text.split('**');
  for (var i = 0; i < parts.length; i++) {
    if (parts[i].isEmpty) continue;
    spans.add(
      pw.TextSpan(
        text: parts[i],
        style: pw.TextStyle(fontWeight: i.isOdd ? pw.FontWeight.bold : pw.FontWeight.normal),
      ),
    );
  }
  return pw.RichText(
    text: pw.TextSpan(
      style: pw.TextStyle(fontSize: fontSize, color: color ?? ProposalColors.ink, lineSpacing: 2),
      children: spans,
    ),
  );
}

/// The "Note that:" panel under the budget table: a navy title bar, then one
/// numbered row per note. A short lead-in before a colon ("Payment terms:")
/// is picked out in bold navy. Each row is its own widget so a long list can
/// run onto the next page.
List<pw.Widget> proposalNotesPanel(List<String> notes) {
  if (notes.isEmpty) return const [];

  const side = pw.BorderSide(color: ProposalColors.border, width: 0.8);

  pw.Widget text(String note) {
    final colon = note.indexOf(':');
    final hasLead = colon >= 3 && colon <= 34 && !note.substring(0, colon).contains('.');
    final style = pw.TextStyle(fontSize: 9, color: ProposalColors.ink, lineSpacing: 2);
    if (!hasLead) return pw.Text(note, style: style);
    return pw.RichText(
      text: pw.TextSpan(
        style: style,
        children: [
          pw.TextSpan(
            text: note.substring(0, colon + 1),
            style: pw.TextStyle(fontWeight: pw.FontWeight.bold, color: ProposalColors.navy),
          ),
          pw.TextSpan(text: note.substring(colon + 1)),
        ],
      ),
    );
  }

  return [
    pw.Container(
      width: double.infinity,
      padding: const pw.EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: const pw.BoxDecoration(
        color: ProposalColors.navy,
        borderRadius: pw.BorderRadius.only(topLeft: pw.Radius.circular(6), topRight: pw.Radius.circular(6)),
      ),
      child: pw.Row(
        children: [
          pw.Container(width: 5, height: 14, color: ProposalColors.green),
          pw.SizedBox(width: 8),
          pw.Text(
            'NOTE THAT',
            style: pw.TextStyle(
              fontSize: 10,
              fontWeight: pw.FontWeight.bold,
              color: PdfColors.white,
              letterSpacing: 1.2,
            ),
          ),
        ],
      ),
    ),
    for (var i = 0; i < notes.length; i++)
      pw.Container(
        width: double.infinity,
        padding: const pw.EdgeInsets.symmetric(horizontal: 12, vertical: 5),
        decoration: pw.BoxDecoration(
          color: i.isOdd ? ProposalColors.rowAlt : PdfColors.white,
          border: const pw.Border(left: side, right: side, bottom: side),
        ),
        child: pw.Row(
          crossAxisAlignment: pw.CrossAxisAlignment.start,
          children: [
            pw.Container(
              width: 16,
              height: 16,
              margin: const pw.EdgeInsets.only(right: 10, top: 0.5),
              alignment: pw.Alignment.center,
              decoration: const pw.BoxDecoration(color: ProposalColors.green, shape: pw.BoxShape.circle),
              child: pw.Text(
                '${i + 1}',
                style: pw.TextStyle(fontSize: 7.5, fontWeight: pw.FontWeight.bold, color: PdfColors.white),
              ),
            ),
            pw.Expanded(child: text(notes[i])),
          ],
        ),
      ),
  ];
}
