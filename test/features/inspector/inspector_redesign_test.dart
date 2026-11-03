import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/features/inspector/presentation/widgets/inspector_task_card.dart';
import 'package:inspecta/features/machines/domain/entities/job_machines.dart';
import 'package:inspecta/features/machines/domain/entities/machine_status.dart';
import 'package:inspecta/features/machines/presentation/widgets/machine_widgets.dart';
import 'package:inspecta/features/requests/domain/entities/inspection_request.dart';
import 'package:inspecta/features/requests/domain/entities/request_source.dart';
import 'package:inspecta/l10n/app_localizations.dart';
import 'package:intl/date_symbol_data_local.dart';

Widget _app(Widget child) => MaterialApp(
  localizationsDelegates: const [
    AppLocalizations.delegate,
    GlobalMaterialLocalizations.delegate,
    GlobalWidgetsLocalizations.delegate,
    GlobalCupertinoLocalizations.delegate,
  ],
  supportedLocales: AppLocalizations.supportedLocales,
  home: Scaffold(body: SingleChildScrollView(child: Padding(padding: const EdgeInsets.all(16), child: child))),
);

InspectionRequest _task(JobStatus status) => InspectionRequest(
  id: 'abc',
  source: RequestSource.email,
  clientName: 'ElRagab Heavy steal',
  subject: 'QT-2026-1668A2 rev01',
  receivedAt: DateTime(2026, 11, 1),
  location: 'smouha',
  status: status,
  items: const [],
  scheduledAt: DateTime(2026, 11, 3, 9, 30),
  certificates: const CertificateCounts(total: 1),
);

const _machine = MachineSummary(
  requestItemId: 'm1',
  name: 'Forklift',
  quantity: 1,
  status: MachineStatus.notStarted,
  serialNumber: 'N84-4856',
  capacity: '5 Tons',
  manufacturer: 'Kronoes',
  model: 'M781',
  checklistTotal: 35,
  inspectionStandards: ['BS EN ISO 3691-1:2011', 'BS ISO 2330:2002'],
);

void main() {
  setUpAll(() async => initializeDateFormatting('en'));

  void phone(WidgetTester tester) {
    tester.view.physicalSize = const Size(390 * 3, 844 * 3);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
  }

  testWidgets('an assigned task shows the quote number, client, time and Start inspection', (tester) async {
    phone(tester);
    await tester.pumpWidget(_app(InspectorTaskCard(task: _task(JobStatus.assigned), onOpen: () {}, onNavigate: () {})));
    expect(find.text('QT-2026-1668A2 rev01'), findsOneWidget);
    expect(find.text('ElRagab Heavy steal'), findsOneWidget);
    expect(find.text('09:30'), findsOneWidget);
    expect(find.text('Assigned'), findsOneWidget);
    expect(find.text('0 of 1 submitted'), findsOneWidget);
    expect(find.text('Directions'), findsOneWidget);
    expect(find.text('Start inspection'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('a task in progress says Continue certificate', (tester) async {
    phone(tester);
    await tester.pumpWidget(_app(InspectorTaskCard(task: _task(JobStatus.inProgress), onOpen: () {}, onNavigate: () {})));
    expect(find.text('Continue certificate'), findsOneWidget);
    expect(find.text('Start inspection'), findsNothing);
  });

  testWidgets('a locked machine keeps full contrast and shows its tags and one lock line', (tester) async {
    phone(tester);
    await tester.pumpWidget(_app(MachineCard(machine: _machine, locked: true, onTap: () {})));
    expect(find.byType(Opacity), findsNothing);
    expect(find.text('5 Tons · Kronoes · M781'), findsOneWidget);
    expect(find.text('S/N N84-4856'), findsOneWidget);
    expect(find.text('Qty 1'), findsOneWidget);
    expect(find.text('BS EN ISO 3691-1:2011'), findsOneWidget);
    expect(find.text('Checklist'), findsOneWidget);
    expect(find.text('Not started'), findsOneWidget);
    expect(find.text('Available after you start the inspection'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
