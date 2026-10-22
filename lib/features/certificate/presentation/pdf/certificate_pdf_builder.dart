import 'dart:typed_data';

import 'package:flutter/services.dart' show rootBundle;
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;

import '../../../requests/domain/entities/inspection_request.dart';
import '../../domain/certificate_number.dart';
import '../../domain/certificate_template.dart';
import '../../domain/certificate_verification.dart';
import '../../domain/entities/certificate.dart';
import '../../domain/entities/certificate_result.dart';
import '../../domain/entities/checklist_answer.dart';

/// Renders a certificate as Integrity International Inspection's one-page
/// "Report of Thorough Examination" — the same letterhead, tables and
/// wording as the paper form, whatever language the app is in (it's a
/// document for the client, not UI). Blank optional fields print as the form's usual
/// "N/A" / "NONE".
Future<Uint8List> buildCertificatePdf(
    {required InspectionRequest request, required Certificate certificate}) async {
  // Inter for the form itself (Cairo's line boxes are too tall to fit the
  // one-page layout), Cairo behind it for any Arabic the inspector typed.
  final regular = pw.Font.ttf(
      await rootBundle.load('assets/fonts/inter/Inter-Regular.ttf'));
  final bold = pw.Font.ttf(
      await rootBundle.load('assets/fonts/inter/Inter-Bold.ttf'));
  final arabic = pw.Font.ttf(
      await rootBundle.load('assets/fonts/cairo/Cairo-Regular.ttf'));
  final arabicBold = pw.Font.ttf(
      await rootBundle.load('assets/fonts/cairo/Cairo-Bold.ttf'));
  final logo = await _image('assets/pngs/integraty-logo.png');
  final accreditation = await _image('assets/pngs/accreditation_egac_iso.png');

  final report = _Report(request, certificate);
  final doc = pw.Document(
    title: 'Report of thorough examination ${report.number}',
    author: _issuerName,
    creator: _issuerName,
  );

  doc.addPage(
    pw.MultiPage(
      pageTheme: pw.PageTheme(
        pageFormat: PdfPageFormat.a4,
        margin: const pw.EdgeInsets.fromLTRB(
            _pageMargin + 6, _pageMargin + 2, _pageMargin + 6, _pageMargin),
        theme: pw.ThemeData.withFont(
            base: regular, bold: bold, fontFallback: [arabic, arabicBold]),
        buildBackground: (_) =>
            pw.FullPage(
              ignoreMargins: true,
              child: pw.Padding(
                padding: const pw.EdgeInsets.all(_pageMargin - 6),
                child: pw.Container(decoration: pw.BoxDecoration(
                    border: pw.Border.all(width: 0.8))),
              ),
            ),
      ),
      header: (context) =>
      context.pageNumber == 1 ? _header(logo) : pw.SizedBox(),
      footer: (_) => _footer(accreditation),
      build: (_) => [_title(), pw.SizedBox(height: 6), ..._body(report)],
    ),
  );

  return doc.save();
}

Future<pw.MemoryImage> _image(String asset) async =>
    pw.MemoryImage((await rootBundle.load(asset)).buffer.asUint8List());

// ---------------------------------------------------------------------------
// The issuer's fixed details — printed on every certificate.

const _issuerName = 'Integrity International Inspection';

/// The name the report is signed for and the unique certificate number is
/// issued under — as it appears on the paper form.
const _issuerLegalName = 'Integrity for inspection and Engineering Solutions';

/// Who signs "For $_issuerLegalName" — not tracked per certificate in the
/// app, so it's the same person on every one.
const _issuerSignatory = 'Ahmed Khalil';

/// The e-mail addresses on the letterhead.
const _issuerEmails = [
  'Ahmed.khalil@integrity-intl.com',
  'Ibrahim.ashraf@integrity-intl.com'
];

const _letterheadGreen = PdfColor.fromInt(0xFF4EA72E);
const _letterheadBlue = PdfColor.fromInt(0xFF156082);

const _generalNotes = [
  'This Certificate becomes invalid if any Repair/modification is done that affects strength or stability.',
  'Any holder of this document is advised that the information contained hereon is limited to the accessible portions of the item examined only.',
  'Equipment has been correctly installed and fit for the purpose',
  'This Certificate should be signed, stamped and carries unique $_issuerLegalName certificate number',
];

// ---------------------------------------------------------------------------
// Page furniture.

const double _pageMargin = 26;
final _line = pw.BorderSide(width: 0.6);
const _grey = PdfColor.fromInt(0xFFE7E6E6);
const _red = PdfColor.fromInt(0xFFFF0000);

pw.Widget _header(pw.MemoryImage logo) {
  pw.TextStyle style({bool bold = false, PdfColor? color}) =>
      pw.TextStyle(fontSize: 8,
          fontWeight: bold ? pw.FontWeight.bold : pw.FontWeight.normal,
          color: color);
  pw.Widget email(String address) =>
      pw.Row(
        mainAxisSize: pw.MainAxisSize.min,
        children: [
          pw.Text('Email: ', style: style(bold: true)),
          pw.UrlLink(
            destination: 'mailto:$address',
            child: pw.Text(
              address,
              style: style(bold: true, color: _letterheadBlue).copyWith(
                  decoration: pw.TextDecoration.underline),
            ),
          ),
        ],
      );
  return pw.Row(
    crossAxisAlignment: pw.CrossAxisAlignment.start,
    children: [
      pw.Expanded(
        child: pw.Column(
          mainAxisSize: pw.MainAxisSize.min,
          crossAxisAlignment: pw.CrossAxisAlignment.start,
          children: [
            pw.Text('$_issuerName.', style: style(bold: true)),
            pw.Text('Industrial & Energy Services.', style: style()),
            pw.Text('21 Nakib Sharif Ramzy st, Smouha, Alex., Egypt.',
                style: style()),
            pw.Row(
              mainAxisSize: pw.MainAxisSize.min,
              children: [
                pw.Text('Mobile No: ', style: style(bold: true)),
                pw.Text('+201000914066 - 01034462363',
                    style: style(bold: true, color: _letterheadGreen)),
              ],
            ),
            for (final address in _issuerEmails) email(address),
          ],
        ),
      ),
      pw.Image(logo, width: 66, height: 62, fit: pw.BoxFit.contain),
    ],
  );
}

pw.Widget _title() =>
    pw.Center(
      child: pw.Text(
        'REPORT OF THOROUGH EXAMINATION',
        style: pw.TextStyle(
            fontSize: 13, fontWeight: pw.FontWeight.bold, color: _red),
      ),
    );

pw.Widget _footer(pw.MemoryImage accreditation) {
  pw.TextStyle style([PdfColor? color]) =>
      pw.TextStyle(fontSize: 6.5, fontWeight: pw.FontWeight.bold, color: color);
  return pw.Padding(
    padding: const pw.EdgeInsets.only(top: 8),
    child: pw.Row(
      crossAxisAlignment: pw.CrossAxisAlignment.end,
      children: [
        pw.Expanded(
          child: pw.Column(
            mainAxisSize: pw.MainAxisSize.min,
            crossAxisAlignment: pw.CrossAxisAlignment.start,
            children: [
              pw.Text('$_issuerName. All rights reserved.', style: style()),
              pw.Text(
                  '$_issuerName | 21 Nakib Sharif Ramzy St., Smouha, Alexandria, Egypt',
                  style: style()),
              pw.Text('Tel: +2 01034462363 | +20 100 091 4066',
                  style: style(const PdfColor.fromInt(0xFF00B050))),
              pw.Text(
                'Email: Ahmed.khalil@integrity-intl.com / Info@integrity-intl.com',
                style: style(const PdfColor.fromInt(0xFF0000FF)),
              ),
              pw.Text(
                'This Certificate is confidential and intended solely for the use of the client for whom it was prepared.',
                style: style(),
              ),
              pw.Text(
                  'Unauthorized use, reproduction, or distribution is strictly prohibited.',
                  style: style()),
            ],
          ),
        ),
        pw.SizedBox(width: 8),
        pw.Image(accreditation, width: 138, height: 34),
      ],
    ),
  );
}

// ---------------------------------------------------------------------------
// The report's values, as printed.

class _Report {
  final InspectionRequest request;
  final Certificate c;

  _Report(this.request, this.c);

  /// dd/MM/yyyy, by hand: the printed certificate is the same whatever
  /// language the app is in (an intl 'ar' format would print Arabic-Indic
  /// digits, and 'en' data isn't guaranteed to be loaded).
  static String _format(DateTime d) =>
      '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(
          2, '0')}/${d.year}';

  String get number => certNumberFor(request);

  String text(String key, [String fallback = 'N/A']) => c.text(key) ?? fallback;

  String date(String key) {
    final value = c.dateOf(key);
    return value == null ? 'N/A' : _format(value);
  }

  /// When the certificate was issued: approval, else submission, else now
  /// (a draft being previewed).
  String get issued => _format(c.reviewedAt ?? c.submittedAt ?? DateTime.now());

  String get item =>
      request.items.isEmpty ? request.equipmentTitle : request.items.first.type;

  String get functionCheck =>
      switch (c.functionCheck) {
        ChecklistAnswer.pass => 'Pass',
        ChecklistAnswer.fail => 'Fail',
        ChecklistAnswer.na || ChecklistAnswer.unanswered => 'N/A',
      };

  String get conclusion =>
      switch (c.finalResult) {
        CertificateResult.safeToOperate => 'Satisfactory',
        CertificateResult.safeWithConditions => 'Satisfactory with conditions',
        CertificateResult.notSafe => 'Not satisfactory',
        null => 'N/A',
      };

  /// What the QR code carries: the link stored when the technical manager
  /// approved (the same one the dashboard shows), or — for a draft still being
  /// previewed — a plain summary, since there's no public page for it yet.
  String get qrData => c.qrData ?? _draftSummary;

  String get _draftSummary => [
    _issuerName,
    'Certificate no: $number',
    'Item: $item',
    'Serial: ${text(CertText.serialNumber)}',
    'Examination: ${date(CertDate.examination)}',
  ].join('\n');
}

/// The link for [request]'s certificate QR once [verificationCode] has been
/// issued for it. Written to the certificate on approval so the PDF and the
/// management dashboard both draw the same code, and a client who scans it
/// opens their certificate on the dashboard.
String certificateQrData({required InspectionRequest request, required String verificationCode}) =>
    certificateVerifyUrl(requestId: request.id, verificationCode: verificationCode);

/// What the public verification page shows beside the certificate — the job's
/// client, location and equipment — copied onto the certificate at approval.
Map<String, String> certificateIssuedFor({
  required InspectionRequest request,
  required Certificate certificate,
}) {
  final report = _Report(request, certificate);
  return {'clientName': request.clientName, 'location': request.location, 'item': report.item};
}

// ---------------------------------------------------------------------------
// Body.

List<pw.Widget> _body(_Report r) {
  final c = r.c;
  final futureDanger = c.answerOf(CertQuestion.futureDanger);
  return [
    // Client / dates.
    _table(
      topLine: true,
      flex: const [483, 215, 215, 215],
      rows: [
        [
          _head('Client and Location of thorough examination'),
          _head('Client representative'),
          _head('Certificate number'),
          _head('Certificate date'),
        ],
        [
          _clientCell(r.request),
          _value(r.text(CertText.clientRepresentative)),
          _value(r.number),
          _value(r.issued)
        ],
      ],
    ),
    _table(
      flex: const [243, 240, 430, 215],
      rows: [
        [
          _head('Through examination date'),
          _head('Next examination date'),
          _head('Standard of inspection'),
          _head('Test type'),
        ],
        [
          _value(r.date(CertDate.examination)),
          _value(r.date(CertDate.nextExamination)),
          _value(r.text(CertText.standardOfInspection), bold: false),
          _value(r.text(CertText.testType)),
        ],
      ],
    ),

    // Item information.
    _table(
      flex: const [1],
      rows: [
        [_head('Item information / Test results.')],
      ],
    ),
    _table(
      flex: const [313, 285, 277, 253],
      rows: [
        [
          _label('Inspected Item:'),
          _plain(r.item),
          _label('Manufacturer:'),
          _plain(r.text(CertText.manufacturer), center: true),
        ],
        [
          _label('Model/ year of manufacturing:'),
          _plain(r.text(CertText.modelYear)),
          _label('MAX. working rate:'),
          _plain(r.text(CertText.maxWorkingRate), center: true),
        ],
        [
          _label('Serial number / Chassis number:'),
          _plain(r.text(CertText.serialNumber)),
          _label('Function Check:'),
          _plain(r.functionCheck, center: true),
        ],
        [
          _label('Owner ID:'),
          _plain(r.text(CertText.ownerId)),
          _label('NDT:'),
          _plain(r.text(CertText.ndt), center: true),
        ],
      ],
    ),
    _table(
      flex: const [598, 530],
      fill: _grey,
      rows: [
        [_label('Conclusion / remarks:'), _value(r.conclusion)],
      ],
    ),
    pw.SizedBox(height: 8),

    // The yes/no questions.
    _questions(c),

    // Defects.
    _fullWidth(
      _text(
        'Identification of any part found to have a defect which is or could become a danger to persons and a description of the defect: ${r
            .text(CertText.defectDescription, 'NONE')}',
      ),
    ),
    _yesNoLine(
      'Is the above an existing or imminent danger to persons *Note-This is a reportable defect',
      c.answerOf(CertQuestion.existingDanger),
    ),
    _table(
      flex: const [10, 9],
      rows: [
        [
          _plain(
            'Is the above a defect which is not yet but could become a danger to persons:(If YES state the date by when)',
          ),
          _plain(futureDanger == true ? 'YES by:  ${r.date(
              CertDate.futureDangerBy)}' : 'YES by:  N/A'),
        ],
      ],
    ),
    _fullWidth(
      _text(
        'Particulars of any repair, renewal or alteration required to remedy the defect identified above: ${r
            .text(CertText.repairsRequired)}',
      ),
    ),
    _fullWidth(
      _text(
        'Particulars of any tests carried out as part of the examination: ${r
            .text(CertText.testsCarriedOut, 'NONE')}',
      ),
    ),
    _fullWidth(_text('Note: ${r.text(CertText.note, '')}'), minHeight: 40),

    // General notes.
    _generalNotesBlock(),

    // Who made and signed the report.
    _signatures(r),
  ];
}

// ---------------------------------------------------------------------------
// Cells and tables.

pw.TextStyle _style({bool bold = false, double size = 9}) =>
    pw.TextStyle(fontSize: size,
        fontWeight: bold ? pw.FontWeight.bold : pw.FontWeight.normal);

pw.Widget _text(String value,
    {bool bold = false, double size = 9, pw.TextAlign align = pw.TextAlign
        .left}) =>
    pw.Text(
      value,
      style: _style(bold: bold, size: size),
      textAlign: align,
    );

/// Column heading — bold, centred.
pw.Widget _head(String value) =>
    _text(value, bold: true, align: pw.TextAlign.center);

/// A value under a heading — centred, bold unless [bold] is off.
pw.Widget _value(String value, {bool bold = true}) =>
    _text(value, bold: bold, align: pw.TextAlign.center);

pw.Widget _label(String value) => _text(value, bold: true);

pw.Widget _plain(String value, {bool center = false}) =>
    _text(value, align: center ? pw.TextAlign.center : pw.TextAlign.left);

pw.Widget _clientCell(InspectionRequest request) =>
    pw.Column(
      mainAxisSize: pw.MainAxisSize.min,
      children: [
        _text(request.clientName, bold: true, align: pw.TextAlign.center),
        if (request.location.isNotEmpty) _text(request.location, bold: true,
            size: 7.5,
            align: pw.TextAlign.center),
      ],
    );

/// One band of the report as a table whose columns are [flex]-proportioned.
/// Tables stack with a shared edge, so only the first one draws its top line.
pw.Widget _table({required List<int> flex, required List<
    List<pw.Widget>> rows, bool topLine = false, PdfColor? fill}) {
  return pw.Table(
    border: pw.TableBorder(
      left: _line,
      right: _line,
      bottom: _line,
      top: topLine ? _line : pw.BorderSide.none,
      horizontalInside: _line,
      verticalInside: _line,
    ),
    columnWidths: {
      for (var i = 0; i < flex.length; i++) i: pw.FlexColumnWidth(
          flex[i].toDouble())
    },
    defaultVerticalAlignment: pw.TableCellVerticalAlignment.middle,
    children: [
      for (final row in rows)
        pw.TableRow(
          decoration: fill == null ? null : pw.BoxDecoration(color: fill),
          children: [
            for (final cell in row)
              pw.Padding(padding: const pw.EdgeInsets.symmetric(
                  horizontal: 5, vertical: 2.5), child: cell),
          ],
        ),
    ],
  );
}

/// A single full-width row of text.
pw.Widget _fullWidth(pw.Widget child, {double? minHeight}) =>
    pw.Container(
      width: double.infinity,
      constraints: minHeight == null ? null : pw.BoxConstraints(
          minHeight: minHeight),
      padding: const pw.EdgeInsets.symmetric(horizontal: 5, vertical: 2.5),
      decoration: pw.BoxDecoration(
        border: pw.Border(left: _line, right: _line, bottom: _line),
      ),
      child: child,
    );

pw.Widget _tick() =>
    pw.CustomPaint(
      size: const PdfPoint(9, 9),
      painter: (canvas, size) {
        canvas
          ..setStrokeColor(PdfColors.black)
          ..setLineWidth(1.3)
          ..moveTo(0.5, size.y * 0.5)
          ..lineTo(size.x * 0.38, 0.5)..lineTo(size.x - 0.5, size.y - 0.5)
          ..strokePath();
      },
    );

/// "YES [ ] NO [ ]" with the chosen box ticked — `null` leaves both empty.
pw.Widget _yesNoBoxes(bool? answer, {double height = 22}) {
  pw.Widget cell(pw.Widget child, int flex) =>
      pw.Expanded(
        flex: flex,
        child: pw.Container(
          height: height,
          alignment: pw.Alignment.center,
          decoration: pw.BoxDecoration(
            border: pw.Border(left: _line, bottom: _line, top: _line),
          ),
          child: child,
        ),
      );
  return pw.Container(
    decoration: pw.BoxDecoration(border: pw.Border(right: _line)),
    child: pw.Row(
      children: [
        cell(_text('YES', size: 9.5), 63),
        cell(answer == true ? _tick() : pw.SizedBox(), 40),
        cell(_text('NO', size: 9.5), 53),
        cell(answer == false ? _tick() : pw.SizedBox(), 52),
      ],
    ),
  );
}

/// A full-width line of text with its YES / NO boxes at the right end.
pw.Widget _yesNoLine(String question, bool? answer) {
  return pw.Container(
    decoration: pw.BoxDecoration(
      border: pw.Border(left: _line, right: _line, bottom: _line),
    ),
    child: pw.Row(
      children: [
        pw.Expanded(
          flex: 757,
          child: pw.Padding(
            padding: const pw.EdgeInsets.symmetric(
                horizontal: 5, vertical: 2.5),
            child: _text(question),
          ),
        ),
        pw.Expanded(flex: 215, child: _yesNoBoxes(answer, height: 20)),
      ],
    ),
  );
}

/// The six yes/no questions — the first two on the left, the four
/// "was the examination carried out" ones on the right.
pw.Widget _questions(Certificate c) {
  const rowHeight = 22.0;
  const headerHeight = 22.0;
  final first = c.answerOf(CertQuestion.firstExamination);
  final installedAsked = first == true;

  pw.Widget leftBlock(String question, bool? answer,
      {required double height, bool enabled = true}) =>
      pw.SizedBox(
        height: height,
        child: pw.Row(
          children: [
            pw.Expanded(
              flex: 386,
              child: pw.Padding(
                  padding: const pw.EdgeInsets.fromLTRB(6, 0, 6, 0),
                  child: _text(question)),
            ),
            pw.Expanded(flex: 211,
                child: pw.Center(child: _yesNoBoxes(enabled ? answer : null))),
          ],
        ),
      );

  pw.Widget rightRow(String question, bool? answer) =>
      pw.Row(
        children: [
          pw.Expanded(
            flex: 335,
            child: pw.Container(
              height: rowHeight,
              alignment: pw.Alignment.centerLeft,
              padding: const pw.EdgeInsets.symmetric(horizontal: 6),
              child: _text(question),
            ),
          ),
          pw.Expanded(flex: 190, child: _yesNoBoxes(answer)),
        ],
      );

  // Separated from the table above by a gap, so it draws its own top line.
  return pw.Container(
    decoration: pw.BoxDecoration(
      border: pw.Border(left: _line, top: _line, right: _line, bottom: _line),
    ),
    child: pw.Row(
      crossAxisAlignment: pw.CrossAxisAlignment.start,
      children: [
        pw.Expanded(
          flex: 597,
          child: pw.Container(
            decoration: pw.BoxDecoration(border: pw.Border(right: _line)),
            child: pw.Column(
              mainAxisSize: pw.MainAxisSize.min,
              children: [
                leftBlock(
                  'Is this the first examination after installation or assembly at a new site or location?',
                  first,
                  height: headerHeight + rowHeight * 2,
                ),
                leftBlock(
                  'If the answer to the above question is YES has the equipment been installed correctly?',
                  c.answerOf(CertQuestion.installedCorrectly),
                  height: rowHeight * 2,
                  enabled: installedAsked,
                ),
              ],
            ),
          ),
        ),
        pw.Expanded(
          flex: 525,
          child: pw.Column(
            mainAxisSize: pw.MainAxisSize.min,
            children: [
              pw.Container(
                height: headerHeight,
                alignment: pw.Alignment.centerLeft,
                padding: const pw.EdgeInsets.symmetric(horizontal: 6),
                child: _text('Was the examination carried out:'),
              ),
              rightRow('Within an interval of 6 months?',
                  c.answerOf(CertQuestion.within6Months)),
              rightRow('Within an interval of 12 months?',
                  c.answerOf(CertQuestion.within12Months)),
              rightRow('In accordance with an examination scheme?',
                  c.answerOf(CertQuestion.examinationScheme)),
              rightRow(
                'After the occurrence of exceptional circumstances?',
                c.answerOf(CertQuestion.exceptionalCircumstances),
              ),
            ],
          ),
        ),
      ],
    ),
  );
}

pw.Widget _generalNotesBlock() {
  return pw.Container(
    decoration: pw.BoxDecoration(
      border: pw.Border(left: _line, right: _line, bottom: _line),
    ),
    child: pw.Row(
      crossAxisAlignment: pw.CrossAxisAlignment.start,
      children: [
        pw.Expanded(
          flex: 275,
          child: pw.Container(
            alignment: pw.Alignment.center,
            padding: const pw.EdgeInsets.symmetric(horizontal: 6, vertical: 22),
            decoration: pw.BoxDecoration(border: pw.Border(right: _line)),
            child: _text('General Notes and Recommendations', size: 8.5,
                align: pw.TextAlign.center),
          ),
        ),
        pw.Expanded(
          flex: 853,
          child: pw.Padding(
            padding: const pw.EdgeInsets.fromLTRB(8, 4, 6, 4),
            child: pw.Column(
              mainAxisSize: pw.MainAxisSize.min,
              crossAxisAlignment: pw.CrossAxisAlignment.start,
              children: [
                for (final note in _generalNotes)
                  pw.Row(
                    crossAxisAlignment: pw.CrossAxisAlignment.start,
                    children: [
                      pw.Padding(
                        padding: const pw.EdgeInsets.only(top: 3, right: 6),
                        child: pw.Container(
                          width: 3,
                          height: 3,
                          decoration: const pw.BoxDecoration(
                              color: PdfColors.black, shape: pw.BoxShape
                              .circle),
                        ),
                      ),
                      pw.Expanded(child: _text(note, size: 7.5)),
                    ],
                  ),
              ],
            ),
          ),
        ),
      ],
    ),
  );
}

pw.Widget _signatures(_Report r) {
  final c = r.c;
  final preparedBy = [
    r.text(CertText.preparedByName, ''),
    r.text(CertText.preparedByQualifications, ''),
  ].where((s) => s.isNotEmpty).toList();
  final reviewer = [
    c.reviewerName ?? '',
    c.reviewerLicense ?? '',
  ].map((s) => s.trim()).where((s) => s.isNotEmpty).toList();

  // Inseparable: the heading row and the signatures always stay together.
  return pw.Inseparable(
    child: pw.Column(
      mainAxisSize: pw.MainAxisSize.min,
      children: [
        _table(
          flex: const [300, 395, 240, 190],
          rows: [
            [
              _text('Name & Qualifications of person making this report',
                  size: 8.5, align: pw.TextAlign.center),
              _text(
                'Name of person signing or authenticating this report on behalf of the author',
                size: 8.5,
                align: pw.TextAlign.center,
              ),
              _text('For $_issuerLegalName', size: 8.5,
                  align: pw.TextAlign.center),
              _text('QR Code', size: 8.5, align: pw.TextAlign.center),
            ],
          ],
        ),
        pw.Container(
          height: 62,
          decoration: pw.BoxDecoration(
            border: pw.Border(left: _line, right: _line, bottom: _line),
          ),
          child: pw.Row(
            children: [
              pw.Expanded(flex: 300,
                  child: _signatureCell(preparedBy, rightLine: true)),
              pw.Expanded(flex: 395,
                  child: _signatureCell(
                      reviewer, strokes: c.signatureStrokes, rightLine: true)),
              pw.Expanded(flex: 240,
                  child: _signatureCell([_issuerSignatory], rightLine: true)),
              pw.Expanded(
                flex: 190,
                child: pw.Center(
                  child: pw.BarcodeWidget(
                    barcode: pw.Barcode.qrCode(),
                    data: r.qrData,
                    width: 54,
                    height: 54,
                    drawText: false,
                  ),
                ),
              ),
            ],
          ),
        ),
      ],
    ),
  );
}

pw.Widget _signatureCell(List<String> lines,
    {List<List<double>> strokes = const [], bool rightLine = false}) {
  return pw.Container(
    decoration: rightLine
        ? pw.BoxDecoration(border: pw.Border(right: _line))
        : null,
    alignment: pw.Alignment.center,
    padding: const pw.EdgeInsets.symmetric(horizontal: 6, vertical: 3),
    child: pw.Column(
      mainAxisSize: pw.MainAxisSize.min,
      mainAxisAlignment: pw.MainAxisAlignment.center,
      children: [
        if (strokes.any((stroke) => stroke.length >= 4))
          pw.SizedBox(
              width: 130, height: 38, child: _signatureDrawing(strokes)),
        for (var i = 0; i < lines.length; i++)
          _text(lines[i], bold: i == 0,
              size: i == 0 ? 9 : 7.5,
              align: pw.TextAlign.center),
      ],
    ),
  );
}

/// The reviewer's drawn signature — each stroke is a flat 0..1-normalised
/// `[x, y, x, y, ...]` list in screen space (y down), so flip y for the PDF.
pw.Widget _signatureDrawing(List<List<double>> strokes) =>
    pw.CustomPaint(
      size: const PdfPoint(130, 38),
      painter: (canvas, size) {
        canvas
          ..setStrokeColor(PdfColors.blue900)
          ..setLineWidth(1.1)
          ..setLineCap(PdfLineCap.round)
          ..setLineJoin(PdfLineJoin.round);
        for (final stroke in strokes) {
          if (stroke.length < 4) continue;
          canvas.moveTo(stroke[0] * size.x, (1 - stroke[1]) * size.y);
          for (var i = 2; i + 1 < stroke.length; i += 2) {
            canvas.lineTo(stroke[i] * size.x, (1 - stroke[i + 1]) * size.y);
          }
          canvas.strokePath();
        }
      },
    );
