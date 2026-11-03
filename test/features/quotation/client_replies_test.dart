import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/features/quotation/data/models/quotation_model.dart';
import 'package:inspecta/features/quotation/domain/entities/quotation.dart';
import 'package:inspecta/features/quotation/presentation/pages/quotation_detail_page.dart';
import 'package:inspecta/features/quotation/presentation/widgets/client_replies_card.dart';
import 'package:inspecta/features/quotation/presentation/widgets/quote_respond_section.dart';
import 'package:inspecta/l10n/app_localizations.dart';

Map<String, dynamic> _quote({List<Map<String, dynamic>>? replies, bool unread = false}) => {
  'id': 'q1',
  'requestId': 'r1',
  'proposalNumber': 'QT-2026-7CE49D',
  'clientName': 'Al Noor',
  'version': 1,
  'status': 'open',
  'sentAt': '2026-11-03T09:00:00Z',
  'createdAt': '2026-11-03T08:00:00Z',
  'amountPiastres': 100000,
  'items': <Map<String, dynamic>>[],
  'hasUnreadClientReply': unread,
  'clientReplies': replies ?? <Map<String, dynamic>>[],
};

Widget _app(Widget child) => MaterialApp(
  localizationsDelegates: const [
    AppLocalizations.delegate,
    GlobalMaterialLocalizations.delegate,
    GlobalWidgetsLocalizations.delegate,
    GlobalCupertinoLocalizations.delegate,
  ],
  supportedLocales: AppLocalizations.supportedLocales,
  home: Scaffold(body: SingleChildScrollView(child: child)),
);

void main() {
  test('the new status is understood', () {
    expect(JobStatus.fromValue('counter_offer_sent'), JobStatus.counterOfferSent);
    expect(JobStatus.counterOfferSent.value, 'counter_offer_sent');
  });

  test('a quote carries the client replies, their text and file names', () {
    final quote = QuotationModel.fromApiJson(
      _quote(
        unread: true,
        replies: [
          {
            'id': 'm1',
            'quoteId': 'q1',
            'fromAddress': 'buyer@alnoor.com',
            'fromName': 'Al Noor',
            'subject': 'Re: Quotation',
            'receivedAt': '2026-11-03T10:15:00Z',
            'text': '  We need a better price.  ',
            'html': '<script>alert(1)</script>',
            'attachments': [
              {'fileName': 'terms.pdf', 'contentType': 'application/pdf', 'size': 52311},
            ],
          },
        ],
      ),
    ).toEntity()!;

    expect(quote.hasUnreadClientReply, isTrue);
    expect(quote.hasClientReply, isTrue);
    final reply = quote.clientReplies.single;
    expect(reply.sender, 'Al Noor');
    expect(reply.text, 'We need a better price.');
    expect(reply.attachments.single.fileName, 'terms.pdf');
    expect(reply.attachments.single.sizeBytes, 52311);
  });

  test('a quote without replies has none and nothing unread', () {
    final quote = QuotationModel.fromApiJson(_quote()).toEntity()!;
    expect(quote.clientReplies, isEmpty);
    expect(quote.hasClientReply, isFalse);
  });

  test('a list row reports the request status and the unread flag', () {
    final row = QuotationModel.fromApiListJson({
      'id': 'q2',
      'requestId': 'r2',
      'proposalNumber': 'QT-2',
      'clientName': 'Delta',
      'status': 'open',
      'stage': 'sent',
      'requestStatus': 'counter_offer_sent',
      'hasUnreadClientReply': true,
    }).toEntity()!;
    expect(row.requestStatus, JobStatus.counterOfferSent);
    expect(row.hasUnreadClientReply, isTrue);
  });

  testWidgets('the replies card shows what the client wrote and the attachment names', (tester) async {
    final quote = QuotationModel.fromApiJson(
      _quote(
        replies: [
          {
            'id': 'm1',
            'quoteId': 'q1',
            'fromAddress': 'buyer@alnoor.com',
            'fromName': 'Al Noor',
            'subject': 'Re: Quotation',
            'receivedAt': DateTime.now().toUtc().toIso8601String(),
            'text': 'We need a better price.',
            'html': '<b>hidden</b>',
            'attachments': [
              {'fileName': 'terms.pdf', 'contentType': 'application/pdf', 'size': 52311},
            ],
          },
        ],
      ),
    ).toEntity()!;
    await tester.pumpWidget(_app(ClientRepliesCard(replies: quote.clientReplies)));
    expect(find.text('Client replies'), findsOneWidget);
    expect(find.text('We need a better price.'), findsOneWidget);
    expect(find.textContaining('terms.pdf'), findsOneWidget);
    expect(find.textContaining('hidden'), findsNothing);
  });

  Future<void> pumpRespond(WidgetTester tester, Quotation quote) async {
    tester.view.physicalSize = const Size(375 * 3, 900 * 3);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      _app(
        QuoteRespondSection(
          current: quote,
          busy: false,
          onReopen: () {},
          onSendNewPrice: () {},
          onAccept: () {},
          onReject: () {},
          onDismissReply: quote.hasClientReply ? () {} : null,
        ),
      ),
    );
  }

  testWidgets('with a reply the panel offers all four ways to answer', (tester) async {
    final quote = QuotationModel.fromApiJson(_quote(unread: true)).toEntity()!;
    await pumpRespond(tester, quote);
    expect(find.text('Reopen to edit'), findsOneWidget);
    expect(find.text('Accept the offer'), findsOneWidget);
    expect(find.text('Reject the request'), findsOneWidget);
    expect(find.text('Ignore reply'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('without a reply there is nothing to ignore, but the offer can be reopened', (tester) async {
    final quote = QuotationModel.fromApiJson(_quote()).toEntity()!;
    await pumpRespond(tester, quote);
    expect(find.text('Reopen to edit'), findsOneWidget);
    expect(find.text('Ignore reply'), findsNothing);
  });

  test('the quotation screen compiles', () {
    expect(QuotationDetailPage, isNotNull);
  });
}
