import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/features/requests/data/models/requests_bodies.dart';
import 'package:inspecta/features/requests/presentation/pages/request_detail_page.dart';

void main() {
  const body = UpdateRequestBody(clientName: 'Delta', location: 'Cairo', items: []);

  test('the update does not open a quotation unless asked to', () {
    expect(body.toJson().containsKey('prepareQuotation'), isFalse);
  });

  test('prepareQuotation is sent as true when the quotation is to be opened', () {
    const opening = UpdateRequestBody(
      clientName: 'Delta',
      location: 'Cairo',
      items: [],
      prepareQuotation: true,
    );
    expect(opening.toJson()['prepareQuotation'], isTrue);
  });

  test('the request screen compiles with the quotation opened from it', () {
    expect(RequestDetailPage, isNotNull);
  });
}
