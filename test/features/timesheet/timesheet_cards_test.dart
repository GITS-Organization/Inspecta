import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/features/timesheet/domain/entities/timesheet_activity.dart';
import 'package:inspecta/features/timesheet/domain/entities/timesheet_entry.dart';
import 'package:inspecta/features/timesheet/domain/entities/timesheet_entry_status.dart';
import 'package:inspecta/features/timesheet/domain/entities/timesheet_expense.dart';
import 'package:inspecta/features/timesheet/domain/entities/timesheet_row.dart';
import 'package:inspecta/features/timesheet/presentation/widgets/timesheet_task_card.dart';
import 'package:inspecta/features/requests/presentation/widgets/status_chip.dart';
import 'package:inspecta/l10n/app_localizations.dart';

import '../requests/fake_requests_repository.dart';
import 'fake_timesheet_repository.dart';

Future<void> _pump(WidgetTester tester, Widget child) async {
  tester.view.physicalSize = const Size(390, 1600) * 3;
  tester.view.devicePixelRatio = 3;
  addTearDown(tester.view.reset);

  await tester.pumpWidget(
    MaterialApp(
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      home: Scaffold(body: SingleChildScrollView(padding: const EdgeInsets.all(20), child: child)),
    ),
  );
}

// A short job-status label: widget tests draw text in the Ahem font, where
// every glyph is a full em wide, so a long one like "Certificate submitted"
// can crowd a header row that has room to spare in the real font.
final _request = sampleRequest(
  status: JobStatus.taskAccepted,
  scheduledAt: DateTime(2026, 10, 5, 9),
  clientName: 'Delta Steel Co.',
);

// 5.5 h worked at EGP 150 an hour (825.00), with an hour's unpaid break and
// two transportation costs — the busiest entry there is.
final _busyEntry = TimesheetEntry.priced(
  requestId: 'REQ-1',
  inspectorId: 'insp-1',
  activities: {
    TimesheetActivity.inspection: 240,
    TimesheetActivity.reportPreparation: 60,
    TimesheetActivity.waiting: 30,
    TimesheetActivity.unpaidBreak: 60,
  },
  expenses: {TimesheetExpense.internalTransport: 12000, TimesheetExpense.externalTransport: 24000},
  hourlyRatePiastres: 15000,
);

void main() {
  group('TimesheetTaskCard (the inspector\'s)', () {
    Future<void> pumpCard(WidgetTester tester, {TimesheetEntry? entry, VoidCallback? onEdit}) =>
        _pump(tester, TimesheetTaskCard(row: TimesheetRow(request: _request, entry: entry), onEdit: onEdit ?? () {}));

    Future<void> tapText(WidgetTester tester, String text) async {
      await tester.tap(find.text(text));
      await tester.pumpAndSettle();
    }

    group('the job', () {
      testWidgets('says when, where the job has got to, what and for whom', (tester) async {
        await pumpCard(tester);

        expect(find.textContaining('09:00'), findsOneWidget);
        expect(find.textContaining('Task accepted'), findsOneWidget);
        expect(find.text('Overhead crane — 10 t'), findsOneWidget);
        expect(find.textContaining('Delta Steel Co.'), findsOneWidget);
      });

      testWidgets('shows the job\'s own status as plain text, not a second coloured pill', (tester) async {
        await pumpCard(tester, entry: sampleEntry());

        expect(find.byType(StatusChip), findsNothing);
        // The one pill is the entry's review status.
        expect(find.text('Awaiting approval'), findsOneWidget);
      });
    });

    group('with nothing logged', () {
      testWidgets('offers to log time and costs, and nothing else', (tester) async {
        var edits = 0;
        await pumpCard(tester, onEdit: () => edits++);

        await tester.tap(find.text('Log time & costs'));

        expect(edits, 1);
        expect(find.text('Awaiting approval'), findsNothing);
        expect(find.text('Details'), findsNothing);
        expect(find.text('Time spent'), findsNothing);
      });
    });

    group('the summary', () {
      testWidgets('is the net time and the price, with the rate it was worked out at', (tester) async {
        await pumpCard(tester, entry: sampleEntry(minutes: 150, pricePiastres: 37500, hourlyRatePiastres: 15000));

        expect(find.text('Time spent'), findsOneWidget);
        expect(find.text('2h 30m'), findsOneWidget);
        expect(find.text('Price'), findsOneWidget);
        expect(find.text('EGP 375.00'), findsOneWidget);
        expect(find.text('EGP 150.00 / hour'), findsOneWidget);
      });

      testWidgets('says the time is net of the unpaid break', (tester) async {
        await pumpCard(
          tester,
          entry: sampleEntry(activities: {TimesheetActivity.inspection: 240, TimesheetActivity.unpaidBreak: 60}),
        );

        expect(find.text('4h'), findsOneWidget);
        expect(find.text('Excl. 1h break'), findsOneWidget);
      });

      testWidgets('says nothing about a break when there was none', (tester) async {
        await pumpCard(tester, entry: sampleEntry());

        expect(find.textContaining('Excl.'), findsNothing);
      });

      testWidgets('is the total, and how it is made up, once there are transportation costs', (tester) async {
        await pumpCard(tester, entry: _busyEntry);

        // 825.00 price + 360.00 transport.
        expect(find.text('Total'), findsOneWidget);
        expect(find.text('EGP 1,185.00'), findsOneWidget);
        expect(find.text('Price EGP 825.00\n+ EGP 360.00 transport'), findsOneWidget);
      });

      testWidgets('shows a dash rather than EGP 0.00 when there is no money yet', (tester) async {
        await pumpCard(tester, entry: sampleEntry(pricePiastres: 0));

        expect(find.text('—'), findsOneWidget);
        expect(find.text('EGP 0.00'), findsNothing);
      });

      testWidgets('shows no rate for an entry from before rates', (tester) async {
        await pumpCard(tester, entry: sampleEntry());

        expect(find.textContaining('/ hour'), findsNothing);
      });
    });

    group('the details', () {
      testWidgets('are folded away until asked for', (tester) async {
        await pumpCard(tester, entry: _busyEntry);

        expect(find.text('Details'), findsOneWidget);
        expect(find.text('Inspection & load testing'), findsNothing);
        expect(find.text('Internal transportation'), findsNothing);
      });

      testWidgets('lay out the time, the costs and the price as a ledger', (tester) async {
        await pumpCard(tester, entry: _busyEntry);

        await tapText(tester, 'Details');

        // Time, each activity — the break taken off rather than added.
        expect(find.text('Inspection & load testing'), findsOneWidget);
        expect(find.text('Report preparation'), findsOneWidget);
        expect(find.text('Waiting'), findsOneWidget);
        expect(find.text('Unpaid break'), findsOneWidget);
        expect(find.text('− 1h'), findsOneWidget);
        // Costs, in money.
        expect(find.text('Internal transportation'), findsOneWidget);
        expect(find.text('EGP 120.00'), findsOneWidget);
        expect(find.text('External transportation'), findsOneWidget);
        expect(find.text('EGP 240.00'), findsOneWidget);
        // The price, with its working, and what it all comes to.
        expect(find.text('5h 30m × EGP 150.00 per hour'), findsOneWidget);
        expect(find.text('EGP 825.00'), findsOneWidget);
        expect(find.text('Hide details'), findsOneWidget);
      });

      testWidgets('fold away again', (tester) async {
        await pumpCard(tester, entry: _busyEntry);
        await tapText(tester, 'Details');

        await tapText(tester, 'Hide details');

        expect(find.text('Inspection & load testing'), findsNothing);
        expect(find.text('Details'), findsOneWidget);
      });

      testWidgets('leave out activities and costs that have nothing on them', (tester) async {
        await pumpCard(tester, entry: sampleEntry(minutes: 120, hourlyRatePiastres: 15000, pricePiastres: 30000));

        await tapText(tester, 'Details');

        expect(find.text('Inspection & load testing'), findsOneWidget);
        expect(find.text('Waiting'), findsNothing);
        expect(find.text('Unpaid break'), findsNothing);
        expect(find.text('Transportation costs'), findsNothing);
        expect(find.text('Total'), findsNothing);
      });

      testWidgets('show no working for a price typed in before rates existed', (tester) async {
        await pumpCard(tester, entry: sampleEntry(minutes: 120, pricePiastres: 30000));

        await tapText(tester, 'Details');

        expect(find.textContaining('per hour'), findsNothing);
        expect(find.text('EGP 300.00'), findsWidgets);
      });
    });

    group('what can be done', () {
      testWidgets('a pending entry can be edited', (tester) async {
        var edits = 0;
        await pumpCard(tester, entry: sampleEntry(), onEdit: () => edits++);

        await tester.tap(find.text('Edit'));

        expect(edits, 1);
        expect(find.text('Locked'), findsNothing);
      });

      testWidgets('an approved entry is locked: no edit, and details say who approved it', (tester) async {
        await pumpCard(
          tester,
          entry: sampleEntry(status: TimesheetEntryStatus.approved, reviewerName: 'Mona Salem'),
        );

        expect(find.text('Approved'), findsOneWidget);
        expect(find.text('Locked'), findsOneWidget);
        expect(find.text('Edit'), findsNothing);

        await tapText(tester, 'Details');

        expect(find.text('Approved by Mona Salem'), findsOneWidget);
      });

      testWidgets('a returned entry spells out why, without opening the details, and can be edited', (tester) async {
        var edits = 0;
        await pumpCard(
          tester,
          entry: sampleEntry(
            status: TimesheetEntryStatus.returned,
            reviewerName: 'Mona Salem',
            reviewNote: 'Price is too high for a half-day job',
          ),
          onEdit: () => edits++,
        );

        expect(find.text('Returned by Mona Salem'), findsOneWidget);
        expect(find.text('Price is too high for a half-day job'), findsOneWidget);

        await tester.tap(find.text('Edit'));

        expect(edits, 1);
      });
    });
  });
}
