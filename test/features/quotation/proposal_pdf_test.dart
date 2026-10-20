import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/features/quotation/domain/entities/pricing_unit.dart';
import 'package:inspecta/features/quotation/domain/entities/quote_line_item.dart';
import 'package:inspecta/features/quotation/domain/proposal_terms.dart';
import 'package:inspecta/features/quotation/presentation/pdf/proposal_pdf_builder.dart';
import 'package:inspecta/features/quotation/presentation/pdf/proposal_pdf_preview.dart';
import 'package:inspecta/features/requests/domain/entities/inspection_request.dart';
import 'package:inspecta/features/requests/domain/entities/request_item.dart';
import 'package:inspecta/features/requests/domain/entities/request_source.dart';

InspectionRequest _request() => InspectionRequest(
  id: 'REQ-2026-0001',
  source: RequestSource.email,
  clientName: 'Delta Steel Co.',
  receivedAt: DateTime(2026, 9, 7),
  location: 'Jubail',
  status: JobStatus.requestReceived,
  items: const [
    RequestItem(
      id: 'item-1',
      type: 'Mobile crane',
      manufacturer: 'Liebherr',
      model: 'LTM 1100',
      capacity: '100 t',
      prNumber: '13351021',
    ),
    RequestItem(id: 'item-2', type: 'Air receiver', capacity: '3000 L', quantity: 2),
  ],
);

const _lines = [
  QuoteLineItem(requestItemId: 'item-1', type: 'Mobile crane', quantity: 1, unitPricePiastres: 1200000),
  QuoteLineItem(
    requestItemId: 'item-2',
    type: 'Air receiver',
    quantity: 2,
    unitPricePiastres: 850000,
    pricingUnit: PricingUnit.perDay,
  ),
];

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('proposal number is stable per request and carries the revision', () {
    final request = _request();
    final first = proposalNumberFor(request, version: 1);

    expect(first, proposalNumberFor(request, version: 1));
    expect(first, matches(RegExp(r'^INT/\d{3}/2026 rev01$')));
    expect(proposalNumberFor(request, version: 3), endsWith('rev03'));
  });

  test('renders the full ten-page proposal', () async {
    final bytes = await buildProposalPdf(
      request: _request(),
      lines: _lines,
      notes: defaultProposalNotes,
      details: ProposalDetails(
        proposalNumber: 'INT/417/2026 rev01',
        quotationNumber: 'QT-2026-0142',
        date: DateTime(2026, 9, 7),
        validUntil: DateTime(2026, 9, 21),
        attention: 'Eng. Reem',
        preparedByName: 'Ahmed Rafat',
        preparedByTitle: 'Coordinator',
      ),
    );

    expect(String.fromCharCodes(bytes.take(5)), '%PDF-');
    expect(RegExp('/Type */Page[^s]').allMatches(String.fromCharCodes(bytes)).length, 10);

    // Set PROPOSAL_PDF_OUT to look at the result.
    final out = Platform.environment['PROPOSAL_PDF_OUT'];
    if (out != null) File(out).writeAsBytesSync(bytes);
  });
}
