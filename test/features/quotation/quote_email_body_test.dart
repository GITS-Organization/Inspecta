import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/features/quotation/data/models/quote_email_model.dart';
import 'package:inspecta/features/quotation/domain/entities/quote_email.dart';
import 'package:inspecta/features/quotation/presentation/pages/quote_email_page.dart';

void main() {
  test('the suggestion goes out alone: no edits, no attachments', () {
    expect(QuoteEmailModel.toBody(null), isEmpty);
    expect(QuoteEmailModel.toBody(const QuoteEmailEdits()), isEmpty);
  });

  test('added files go in attachments with their name and Base64 content', () {
    final bytes = Uint8List.fromList(utf8.encode('terms'));
    final body = QuoteEmailModel.toBody(
      QuoteEmailEdits(
        sentTo: 'buyer@alnoor.com',
        subject: 'Quotation',
        attachments: [QuoteAttachment(fileName: 'terms.pdf', bytes: bytes)],
      ),
    );
    expect(body['sentTo'], 'buyer@alnoor.com');
    expect(body['attachments'], [
      {'fileName': 'terms.pdf', 'contentBase64': base64Encode(bytes)},
    ]);
  });

  test('a file alone is an edit', () {
    final edits = QuoteEmailEdits(attachments: [QuoteAttachment(fileName: 'a.pdf', bytes: Uint8List(1))]);
    expect(edits.isEmpty, isFalse);
  });

  test('the email page compiles', () {
    expect(QuoteEmailPage, isNotNull);
  });
}
